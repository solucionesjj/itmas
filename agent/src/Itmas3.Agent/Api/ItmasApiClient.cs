using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Itmas3.Agent.Collectors.Model;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Configuration;

namespace Itmas3.Agent.Api;

public sealed record EnrollmentRequest(
    string HardwareId,
    string Hostname,
    string OsFamily,
    string AgentVersion,
    string TermsVersion,
    DateTimeOffset TermsAcceptedAt);

public sealed record EnrollmentResponse(Guid DeviceId, string ApiKey);

public enum SubmitOutcome
{
    Accepted,
    CredentialRejected,
    Failed,

    /// <summary>408, 429 or 5xx: worth retrying the same SQL audit snapshot (same submissionId) shortly.</summary>
    TransientFailure,
}

public sealed class EnrollmentException(string message) : Exception(message);

public interface IItmasApiClient
{
    /// <summary>
    /// Checks that <paramref name="serverUrl"/> is reachable over valid HTTPS and is an ITMas server, before the
    /// authorization code is requested. Returns null when it is, or a message for the user.
    /// </summary>
    Task<string?> CheckServerAsync(Uri serverUrl, CancellationToken cancellationToken);

    Task<EnrollmentResponse> EnrollAsync(Uri serverUrl, string enrollmentCode, EnrollmentRequest request, CancellationToken cancellationToken);

    Task<(SubmitOutcome Outcome, string Detail)> SubmitAsync(Uri serverUrl, string apiKey, InventoryReport report, CancellationToken cancellationToken);

    /// <summary><c>POST /api/v1/sql-audit-snapshots</c> (SPEC-0005 RF-8, §8.2) with the device API key.</summary>
    Task<(SubmitOutcome Outcome, string Detail)> SubmitSqlAuditAsync(Uri serverUrl, string apiKey, SqlAuditSnapshot snapshot, CancellationToken cancellationToken);
}

/// <summary>
/// HTTPS client of the ITMas API (SPEC-0002 §8). TLS is always validated against the OS trust store (no override
/// exists) and the system proxy is used. Credentials travel only in the Authorization header and are never logged.
/// </summary>
public sealed class ItmasApiClient(HttpClient httpClient) : IItmasApiClient
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15);

    public async Task<string?> CheckServerAsync(Uri serverUrl, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CheckTimeout);
        try
        {
            // Public health endpoint of the ITMas web server (nginx), no credentials involved.
            using var response = await httpClient.GetAsync(new Uri(serverUrl, "healthz"), timeout.Token);
            return response.IsSuccessStatusCode
                ? null
                : $"la dirección responde, pero no parece un servidor ITMas (HTTP {(int)response.StatusCode}).";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return "el servidor no respondió a tiempo.";
        }
        catch (HttpRequestException exception)
        {
            return DescribeConnectionError(exception);
        }
    }

    /// <summary>Explains a connection failure in terms the person installing the agent can act on.</summary>
    public static string DescribeConnectionError(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case AuthenticationException:
                    return "el certificado HTTPS del servidor no es válido para esa dirección o no es de confianza. "
                        + "Compruebe que la URL es la del servidor ITMas y que este tiene un certificado válido.";
                case SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData }:
                    return "no se pudo resolver el nombre del servidor. Revise la URL y la conexión a Internet.";
                case SocketException { SocketErrorCode: SocketError.ConnectionRefused }:
                    return "el servidor rechazó la conexión. Revise la URL y el puerto.";
                case SocketException { SocketErrorCode: SocketError.TimedOut or SocketError.HostUnreachable or SocketError.NetworkUnreachable }:
                    return "no hay conexión con el servidor (red inaccesible o tiempo agotado).";
            }
        }

        return exception.Message;
    }

    public async Task<EnrollmentResponse> EnrollAsync(
        Uri serverUrl, string enrollmentCode, EnrollmentRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(serverUrl, "api/v1/device-enrollments"))
        {
            Content = JsonContent.Create(request, AgentJsonContext.Default.EnrollmentRequest),
        };
        Authorize(message, "Enrollment", enrollmentCode);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Created
            && await response.Content.ReadFromJsonAsync(AgentJsonContext.Default.EnrollmentResponse, cancellationToken) is { ApiKey.Length: > 0 } enrollment)
        {
            return enrollment;
        }

        throw new EnrollmentException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "El código de autorización no es válido.",
            HttpStatusCode.NotFound => "El enrolamiento de dispositivos no está habilitado en el servidor.",
            HttpStatusCode.UnprocessableEntity => "El servidor rechazó la solicitud (¿versión de términos o del agente no vigente?).",
            HttpStatusCode.TooManyRequests => "Demasiados intentos. Espere un minuto y vuelva a intentarlo.",
            _ => $"El servidor respondió {(int)response.StatusCode}.",
        });
    }

    public async Task<(SubmitOutcome Outcome, string Detail)> SubmitAsync(
        Uri serverUrl, string apiKey, InventoryReport report, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(serverUrl, "api/v1/inventory-reports"))
        {
            Content = new StringContent(InventoryJson.Serialize(report), Encoding.UTF8, "application/json"),
        };
        Authorize(message, "Device", apiKey);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.Accepted => (SubmitOutcome.Accepted, "202"),
            HttpStatusCode.Unauthorized => (SubmitOutcome.CredentialRejected, "401"),
            _ => (SubmitOutcome.Failed, ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
    }

    public async Task<(SubmitOutcome Outcome, string Detail)> SubmitSqlAuditAsync(
        Uri serverUrl, string apiKey, SqlAuditSnapshot snapshot, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(serverUrl, "api/v1/sql-audit-snapshots"))
        {
            Content = new StringContent(SqlAuditJson.Serialize(snapshot), Encoding.UTF8, "application/json"),
        };
        Authorize(message, "Device", apiKey);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var status = (int)response.StatusCode;
        return response.StatusCode switch
        {
            HttpStatusCode.Accepted => (SubmitOutcome.Accepted, "202"),
            HttpStatusCode.Unauthorized => (SubmitOutcome.CredentialRejected, "401"),
            HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests => (SubmitOutcome.TransientFailure, status.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            _ when status >= 500 => (SubmitOutcome.TransientFailure, status.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            _ => (SubmitOutcome.Failed, status.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
    }

    public static HttpClient CreateHttpClient(string agentVersion)
    {
        // Default SocketsHttpHandler: system proxy and full certificate validation (no callback override).
        var client = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ITMas-Agent", agentVersion));
        return client;
    }

    private static void Authorize(HttpRequestMessage message, string scheme, string credential)
    {
        message.Headers.Authorization = new AuthenticationHeaderValue(scheme, credential);
        // Anti-CSRF header required by the API for state-changing requests (ADR-0009 §5).
        message.Headers.Add("X-CSRF", "1");
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Itmas3.Agent.Collectors.Model;

namespace Itmas3.Agent.Collectors.Common;

public interface IPublicNetworkSource
{
    Task<PublicNetworkInfo> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Public IP and approximate location from ipinfo.io (ADR-0011): anonymous <c>GET https://ipinfo.io/json</c>, no device
/// data sent, TLS validated, 10 s timeout. The response is third-party data: only five fields are taken and validated.
/// </summary>
public sealed partial class PublicNetworkSource(HttpClient httpClient) : IPublicNetworkSource
{
    public static readonly Uri Endpoint = new("https://ipinfo.io/json");
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private const int MaxResponseBytes = 64 * 1024;

    public async Task<PublicNetworkInfo> GetAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new CollectorException(UnavailableReasons.Error, $"ipinfo.io answered {(int)response.StatusCode}.");
            }

            if (response.Content.Headers.ContentLength > MaxResponseBytes)
            {
                throw new CollectorException(UnavailableReasons.Error, "ipinfo.io response is too large.");
            }

            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            return Parse(body.Length <= MaxResponseBytes ? body : throw new CollectorException(UnavailableReasons.Error, "ipinfo.io response is too large."));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CollectorException(UnavailableReasons.Timeout, "ipinfo.io did not answer in time.");
        }
        catch (HttpRequestException exception)
        {
            throw new CollectorException(UnavailableReasons.Error, "ipinfo.io is not reachable.", exception);
        }
    }

    /// <summary>Extracts and validates ip, country, region, city and org from an ipinfo.io JSON document.</summary>
    public static PublicNetworkInfo Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || (root.TryGetProperty("bogon", out var bogon) && bogon.ValueKind == JsonValueKind.True))
            {
                throw Invalid();
            }

            var ip = Text(root, "ip", 45);
            var country = Text(root, "country", 2);
            if (ip is null || !IPAddress.TryParse(ip, out _) || country is null || !CountryPattern().IsMatch(country))
            {
                throw Invalid();
            }

            return new PublicNetworkInfo(ip, country, Text(root, "region"), Text(root, "city"), Text(root, "org"));
        }
        catch (JsonException exception)
        {
            throw new CollectorException(UnavailableReasons.Error, "ipinfo.io returned invalid JSON.", exception);
        }
    }

    private static string? Text(JsonElement root, string name, int maxLength = TextSanitizer.MaxLength) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? TextSanitizer.Clean(value.GetString(), maxLength)
            : null;

    private static CollectorException Invalid() => new(UnavailableReasons.Error, "ipinfo.io returned no usable public IP or country.");

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex CountryPattern();
}

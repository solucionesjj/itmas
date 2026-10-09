using System.Text.Json;
using System.Text.Json.Serialization;

namespace Itmas3.Agent.Configuration;

/// <summary>Non-secret configuration written at install time.</summary>
public sealed record AgentConfiguration(string ServerUrl)
{
    public const string DefaultServerUrl = "https://itmas.sacservices.cloud";

    /// <summary>Only absolute https URLs without credentials, query or fragment (constitution §13.3).</summary>
    public static bool IsValidServerUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrEmpty(uri.Host)
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);

    /// <summary>Canonical form stored in the configuration: trimmed, without trailing slash.</summary>
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Trim().TrimEnd('/');
    }

    /// <summary>Base address for relative API paths (always ends with "/", so a path prefix is preserved).</summary>
    public static Uri BaseUri(string serverUrl) => new(Normalize(serverUrl) + "/", UriKind.Absolute);
}

/// <summary>Persistent state of the scheduler (RF-6 status, RF-8 cadence, RF-10 rejected credential).</summary>
public sealed record AgentState(
    DateTimeOffset? LastSuccessAt = null,
    DateTimeOffset? LastAttemptAt = null,
    DateTimeOffset? NextRunAt = null,
    int ConsecutiveFailures = 0,
    DateTimeOffset? CredentialRejectedAt = null,
    string? LastError = null);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(AgentConfiguration))]
[JsonSerializable(typeof(AgentState))]
[JsonSerializable(typeof(Api.EnrollmentRequest))]
[JsonSerializable(typeof(Api.EnrollmentResponse))]
internal sealed partial class AgentJsonContext : JsonSerializerContext;

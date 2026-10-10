namespace Itmas3.Api.Inventory;

/// <summary>Authentication of devices (ADR-0009). Never default schemes: only the device policies evaluate them.</summary>
public static class DeviceAuthenticationSchemes
{
    /// <summary><c>Authorization: Enrollment &lt;code&gt;</c> — the one-time enrollment code of a registered device (ADR-0012).</summary>
    public const string Enrollment = "Enrollment";

    /// <summary><c>Authorization: Device &lt;apiKey&gt;</c> — the API key issued to one device.</summary>
    public const string Device = "Device";

    /// <summary>Claim with the id of the authenticated device.</summary>
    public const string DeviceIdClaim = "itmas3_device_id";

    /// <summary>Claim with the id of the device that owns the presented enrollment code.</summary>
    public const string EnrollmentClaim = "itmas3_enrollment";

    /// <summary>Returns the credential of <paramref name="scheme"/> from the Authorization header, or null.</summary>
    public static string? ReadCredential(HttpRequest request, string scheme)
    {
        var header = request.Headers.Authorization.ToString();
        var prefix = scheme + " ";
        return header.StartsWith(prefix, StringComparison.Ordinal) && header.Length > prefix.Length
            ? header[prefix.Length..].Trim()
            : null;
    }
}

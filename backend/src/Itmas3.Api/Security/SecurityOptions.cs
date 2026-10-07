namespace Itmas3.Api.Security;

internal sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Redirect HTTP to HTTPS inside the API. Disabled by default because TLS is terminated by the
    /// reverse proxy / ingress in front of the containers (see SECURITY.md).
    /// </summary>
    public bool UseHttpsRedirection { get; set; }
}

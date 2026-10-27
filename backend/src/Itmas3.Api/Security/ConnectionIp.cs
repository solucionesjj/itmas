namespace Itmas3.Api.Security;

/// <summary>
/// Source IP of the request after <c>UseForwardedHeaders</c>: X-Forwarded-For is honoured only from the trusted proxy
/// networks, and nginx rebuilds it from the last address added by the host proxy (ADR-0011, ADR-0018). A remote client cannot
/// set it through that proxy, but any process of the server host that reaches the loopback port is trusted for it (ADR-0018, decision 3).
/// </summary>
internal static class ConnectionIp
{
    public static string? Of(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return null;
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }
}

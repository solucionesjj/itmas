namespace Itmas3.Api.Security;

/// <summary>
/// Source IP of the request after <c>UseForwardedHeaders</c>: X-Forwarded-For is honoured only from the trusted proxy
/// networks, and nginx overwrites it with the real client address (ADR-0011). Clients cannot set it.
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

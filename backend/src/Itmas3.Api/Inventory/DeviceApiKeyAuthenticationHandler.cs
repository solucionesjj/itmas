using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Itmas3.Application.Inventory.AuthenticateDevice;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Itmas3.Api.Inventory;

/// <summary>Authenticates a device by its API key (ADR-0009). The principal carries only the device id.</summary>
internal sealed partial class DeviceApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    DeviceAuthenticator authenticator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var apiKey = DeviceAuthenticationSchemes.ReadCredential(Request, DeviceAuthenticationSchemes.Device);
        if (apiKey is null)
        {
            return AuthenticateResult.NoResult();
        }

        if (await authenticator.AuthenticateAsync(apiKey, Context.RequestAborted) is not { } deviceId)
        {
            LogInvalidDeviceKey(Logger, Context.Connection.RemoteIpAddress?.ToString());
            return AuthenticateResult.Fail("Invalid device credential.");
        }

        var identity = new ClaimsIdentity(
            [new Claim(DeviceAuthenticationSchemes.DeviceIdClaim, deviceId.ToString("D", CultureInfo.InvariantCulture))],
            Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = DeviceAuthenticationSchemes.Device;
        return base.HandleChallengeAsync(properties);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Device request rejected: unknown or revoked API key from {RemoteIp}")]
    private static partial void LogInvalidDeviceKey(ILogger logger, string? remoteIp);
}

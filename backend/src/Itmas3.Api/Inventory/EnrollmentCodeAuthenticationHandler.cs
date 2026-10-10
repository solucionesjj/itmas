using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Itmas3.Application.Inventory.AuthenticateDevice;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Itmas3.Api.Inventory;

/// <summary>
/// Validates the one-time enrollment code of a registered device (ADR-0012). The principal carries the id of the
/// device that owns the code. Unknown, consumed, replaced and deleted codes are indistinguishable (401).
/// </summary>
internal sealed partial class EnrollmentCodeAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    EnrollmentCodeAuthenticator authenticator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var code = DeviceAuthenticationSchemes.ReadCredential(Request, DeviceAuthenticationSchemes.Enrollment);
        if (code is null)
        {
            return AuthenticateResult.NoResult();
        }

        if (await authenticator.AuthenticateAsync(code, Context.RequestAborted) is not { } deviceId)
        {
            LogInvalidEnrollmentCode(Logger, Context.Connection.RemoteIpAddress?.ToString());
            return AuthenticateResult.Fail("Invalid enrollment code.");
        }

        var identity = new ClaimsIdentity(
            [new Claim(DeviceAuthenticationSchemes.EnrollmentClaim, deviceId.ToString("D", CultureInfo.InvariantCulture))],
            Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = DeviceAuthenticationSchemes.Enrollment;
        return base.HandleChallengeAsync(properties);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Device enrollment rejected: unknown, used or revoked enrollment code from {RemoteIp}")]
    private static partial void LogInvalidEnrollmentCode(ILogger logger, string? remoteIp);
}

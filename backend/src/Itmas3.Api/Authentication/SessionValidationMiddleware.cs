using System.Security.Claims;
using Itmas3.Application.Authorization.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Itmas3.Api.Authentication;

/// <summary>
/// On every request, invalidates sessions whose application user no longer exists, is deactivated, or whose security
/// stamp was rotated (SPEC-0001 RF-2, CA-18). The request then continues as anonymous (protected endpoints → 401).
/// Uses the version-keyed snapshot cache, so the check costs one trivial query per request.
/// </summary>
internal sealed partial class SessionValidationMiddleware(RequestDelegate next, ILogger<SessionValidationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IAuthorizationSnapshotProvider snapshots)
    {
        if (context.User.Identity?.IsAuthenticated == true && !await IsValidAsync(context, snapshots))
        {
            LogSessionRejected(logger, new ClaimsCurrentUser(context.User).ApplicationUserId);
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
        }

        await next(context);
    }

    private static async Task<bool> IsValidAsync(HttpContext context, IAuthorizationSnapshotProvider snapshots)
    {
        var user = new ClaimsCurrentUser(context.User);
        if (user.ApplicationUserId is not { } userId || user.SecurityStamp is not { } stamp)
        {
            return false;
        }

        var snapshot = await snapshots.GetAsync(userId, context.RequestAborted);
        return snapshot is { IsActive: true } && string.Equals(snapshot.SecurityStamp, stamp, StringComparison.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Session of user {UserId} is no longer valid and was terminated")]
    private static partial void LogSessionRejected(ILogger logger, Guid? userId);
}

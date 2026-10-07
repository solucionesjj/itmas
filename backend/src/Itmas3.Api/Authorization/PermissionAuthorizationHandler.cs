using Itmas3.Api.Authentication;
using Itmas3.Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Itmas3.Api.Authorization;

internal sealed partial class PermissionAuthorizationHandler(
    IPermissionEvaluator permissionEvaluator,
    ILogger<PermissionAuthorizationHandler> logger) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var user = new ClaimsCurrentUser(context.User);
        if (!user.IsAuthenticated)
        {
            return;
        }

        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;

        if (await permissionEvaluator.IsGrantedAsync(user, requirement.Permission, cancellationToken))
        {
            context.Succeed(requirement);
            return;
        }

        // Pseudonymous identifier only: names and e-mail addresses are never logged.
        LogPermissionDenied(logger, requirement.Permission, user.UserId);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Permission {Permission} denied for user {UserId}")]
    private static partial void LogPermissionDenied(ILogger logger, string permission, string? userId);
}

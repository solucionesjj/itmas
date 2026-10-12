using System.Reflection;
using Itmas3.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.UnitTests.Architecture;

/// <summary>
/// Default deny (constitution §3): every endpoint must state who can call it. Allowed declarations are
/// [AllowAnonymous], [RequirePermission("...")], [Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)] or, only for
/// device endpoints, a device policy (ADR-0009). A bare [Authorize] (any authenticated user) is rejected because it
/// grants privileges implicitly.
/// </summary>
public sealed class EndpointAuthorizationConventionTests
{
    public static TheoryData<string> Actions()
    {
        var data = new TheoryData<string>();
        foreach (var action in ControllerActions())
        {
            data.Add($"{action.DeclaringType!.Name}.{action.Name}");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public void Every_action_declares_explicit_authorization(string actionName)
    {
        var action = ControllerActions().Single(method => $"{method.DeclaringType!.Name}.{method.Name}" == actionName);
        var attributes = action.GetCustomAttributes(inherit: true)
            .Concat(action.DeclaringType!.GetCustomAttributes(inherit: true))
            .ToArray();

        var isAnonymous = attributes.OfType<AllowAnonymousAttribute>().Any();
        var requiresPermission = attributes.OfType<RequirePermissionAttribute>().Any();
        var requiresAuthenticatedUserPolicy = attributes.OfType<AuthorizeAttribute>()
            .Any(attribute => attribute.Policy == AuthorizationPolicies.AuthenticatedUser);
        var requiresDevicePolicy = attributes.OfType<AuthorizeAttribute>()
            .Any(attribute => attribute.Policy is AuthorizationPolicies.DeviceEnrollment or AuthorizationPolicies.EnrolledDevice);
        var hasBareAuthorize = attributes.OfType<AuthorizeAttribute>()
            .Any(attribute => attribute is not RequirePermissionAttribute && attribute.Policy is null && attribute.Roles is null);

        Assert.False(hasBareAuthorize, $"{actionName} uses a bare [Authorize]; declare a permission instead.");
        Assert.True(
            isAnonymous || requiresPermission || requiresAuthenticatedUserPolicy || requiresDevicePolicy,
            $"{actionName} must declare [AllowAnonymous], [RequirePermission], the AuthenticatedUser policy or a device policy.");
    }

    [Fact]
    public void Device_policies_are_only_used_by_device_endpoints()
    {
        var deviceActions = ControllerActions()
            .Where(action => action.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Concat(action.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
                .Any(attribute => attribute.Policy is AuthorizationPolicies.DeviceEnrollment or AuthorizationPolicies.EnrolledDevice))
            .Select(action => action.DeclaringType!.Name)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

        // SPEC-0002 (enrollment, inventories) and SPEC-0005 (SQL audit snapshots).
        Assert.Equal(["DeviceEnrollmentsController", "InventoryReportsController", "SqlAuditSnapshotsController"], deviceActions);
    }

    private static IEnumerable<MethodInfo> ControllerActions() =>
        typeof(Program).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(method => !method.IsSpecialName && method.GetCustomAttribute<NonActionAttribute>() is null);
}

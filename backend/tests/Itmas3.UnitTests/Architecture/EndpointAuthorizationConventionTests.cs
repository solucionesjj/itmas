using System.Reflection;
using Itmas3.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.UnitTests.Architecture;

/// <summary>
/// Default deny (constitution §3): every endpoint must state who can call it. Allowed declarations are
/// [AllowAnonymous], [RequirePermission("...")] or [Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)].
/// A bare [Authorize] (any authenticated user) is rejected because it grants privileges implicitly.
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
        var hasBareAuthorize = attributes.OfType<AuthorizeAttribute>()
            .Any(attribute => attribute is not RequirePermissionAttribute && attribute.Policy is null && attribute.Roles is null);

        Assert.False(hasBareAuthorize, $"{actionName} uses a bare [Authorize]; declare a permission instead.");
        Assert.True(
            isAnonymous || requiresPermission || requiresAuthenticatedUserPolicy,
            $"{actionName} must declare [AllowAnonymous], [RequirePermission] or the AuthenticatedUser policy.");
    }

    private static IEnumerable<MethodInfo> ControllerActions() =>
        typeof(Program).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(method => !method.IsSpecialName && method.GetCustomAttribute<NonActionAttribute>() is null);
}

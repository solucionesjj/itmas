using System.Reflection;
using Itmas3.Api.Authorization;
using Itmas3.Application.Authorization;

namespace Itmas3.UnitTests.Architecture;

/// <summary>Constitution §3.3: every permission enforced by an endpoint is declared in the permission catalog.</summary>
public sealed class PermissionCatalogConventionTests
{
    private static readonly HashSet<string> Catalog = typeof(IPermissionDefinitionProvider).Assembly.GetTypes()
        .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(IPermissionDefinitionProvider).IsAssignableFrom(type))
        .Select(type => (IPermissionDefinitionProvider)Activator.CreateInstance(type, nonPublic: true)!)
        .SelectMany(provider => provider.GetPermissions())
        .Select(declaration => declaration.Name)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Every_required_permission_is_declared()
    {
        var used = typeof(Program).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Cast<MemberInfo>().Append(type))
            .SelectMany(member => member.GetCustomAttributes<RequirePermissionAttribute>())
            .Select(attribute => attribute.Permission)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(used);
        Assert.Empty(used.Except(Catalog));
    }

    [Fact]
    public void Declared_permissions_are_well_formed() =>
        Assert.All(Catalog, permission => Assert.True(PermissionName.IsValid(permission), permission));
}

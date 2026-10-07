namespace Itmas3.Application.Authorization;

/// <summary>A permission declared in code. Its name follows <see cref="PermissionName"/>.</summary>
public sealed record PermissionDeclaration(string Name, string DisplayName, string Description, string Category, bool IsGrantable = true);

/// <summary>
/// Each module contributes the permissions it enforces. The catalog is synchronised to the database at start-up
/// (constitution §3.3: every permission used by an endpoint must be declared here).
/// </summary>
public interface IPermissionDefinitionProvider
{
    IEnumerable<PermissionDeclaration> GetPermissions();
}

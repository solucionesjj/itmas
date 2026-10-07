namespace Itmas3.Application.Authorization;

/// <summary>Permissions of the authorization administration module (SPEC-0001 §7).</summary>
public static class AuthorizationPermissions
{
    public const string Category = "Autorización";

    public const string UsersRead = "users.read";
    public const string UsersUpdate = "users.update";
    public const string UsersPoliciesAssign = "users.policies.assign";
    public const string UsersPoliciesRevoke = "users.policies.revoke";
    public const string PoliciesRead = "policies.read";
    public const string PoliciesCreate = "policies.create";
    public const string PoliciesUpdate = "policies.update";
    public const string PoliciesDelete = "policies.delete";
    public const string PermissionsRead = "permissions.read";
    public const string PermissionsUpdate = "permissions.update";
    public const string AuditRead = "audit.read";

    /// <summary>Not grantable: only Root users can promote or revoke Root.</summary>
    public const string RootManage = "authorization.root.manage";
}

internal sealed class AuthorizationPermissionDefinitions : IPermissionDefinitionProvider
{
    private const string Category = AuthorizationPermissions.Category;

    public IEnumerable<PermissionDeclaration> GetPermissions() =>
    [
        new(AuthorizationPermissions.UsersRead, "Consultar usuarios", "Ver usuarios, sus políticas y sus permisos efectivos.", Category),
        new(AuthorizationPermissions.UsersUpdate, "Activar o desactivar usuarios", "Cambiar el estado activo de los usuarios.", Category),
        new(AuthorizationPermissions.UsersPoliciesAssign, "Asignar políticas", "Asignar políticas a usuarios.", Category),
        new(AuthorizationPermissions.UsersPoliciesRevoke, "Retirar políticas", "Retirar políticas de usuarios.", Category),
        new(AuthorizationPermissions.PoliciesRead, "Consultar políticas", "Ver políticas, sus permisos y sus usuarios.", Category),
        new(AuthorizationPermissions.PoliciesCreate, "Crear políticas", "Crear políticas a partir del catálogo de permisos.", Category),
        new(AuthorizationPermissions.PoliciesUpdate, "Modificar políticas", "Modificar, activar o desactivar políticas y sus permisos.", Category),
        new(AuthorizationPermissions.PoliciesDelete, "Eliminar políticas", "Eliminar políticas sin usuarios asignados.", Category),
        new(AuthorizationPermissions.PermissionsRead, "Consultar permisos", "Ver el catálogo de permisos.", Category),
        new(AuthorizationPermissions.PermissionsUpdate, "Editar permisos", "Editar el nombre visible, la descripción y la categoría de los permisos.", Category),
        new(AuthorizationPermissions.AuditRead, "Consultar auditoría", "Ver el registro de auditoría de autorización.", Category),
        new(AuthorizationPermissions.RootManage, "Administrar Root", "Promover o revocar usuarios Root.", Category, IsGrantable: false),
    ];
}

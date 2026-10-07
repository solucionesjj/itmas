using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Domain.Authorization;

namespace Itmas3.Application.Authorization.Permissions.SynchronizeCatalog;

/// <summary>
/// Use case: align the stored permission catalog with the permissions declared in code (RF-6, RF-7).
/// New permissions are added; missing ones are deprecated (never deleted); editable metadata is not overwritten.
/// Safe to run concurrently from several replicas (serialised by the unit of work).
/// </summary>
public sealed class SynchronizePermissionCatalogHandler(
    IEnumerable<IPermissionDefinitionProvider> providers,
    IPermissionRepository permissions,
    IAuthorizationUnitOfWork unitOfWork)
{
    public Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var declared = Declarations();
        var context = new AuditContext(AuditActions.PermissionSynced, AuditResourceTypes.Permission, null);

        return unitOfWork.ExecuteAsync(context, async (audit, ct) =>
        {
            audit.Actor = AuditActor.System;
            var stored = (await permissions.GetAllAsync(ct)).ToDictionary(permission => permission.Name, StringComparer.Ordinal);

            foreach (var declaration in declared.Values)
            {
                if (!stored.TryGetValue(declaration.Name, out var existing))
                {
                    var permission = PermissionDefinition.Declare(
                        declaration.Name, declaration.DisplayName, declaration.Description, declaration.Category, declaration.IsGrantable);
                    permissions.Add(permission);
                    audit.Record(AuditActions.PermissionSynced, AuditResourceTypes.Permission, permission.Name, null,
                        new { Status = "added", permission.IsGrantable });
                }
                else if (existing.Synchronise(declaration.IsGrantable))
                {
                    audit.Record(AuditActions.PermissionSynced, AuditResourceTypes.Permission, existing.Name, null,
                        new { Status = "updated", existing.IsGrantable });
                }
            }

            foreach (var obsolete in stored.Values.Where(permission => !declared.ContainsKey(permission.Name)))
            {
                if (obsolete.Deprecate())
                {
                    audit.Record(AuditActions.PermissionSynced, AuditResourceTypes.Permission, obsolete.Name, null, new { Status = "deprecated" });
                }
            }

            return audit.Entries.Count;
        }, cancellationToken);
    }

    private Dictionary<string, PermissionDeclaration> Declarations()
    {
        var declared = new Dictionary<string, PermissionDeclaration>(StringComparer.Ordinal);
        foreach (var declaration in providers.SelectMany(provider => provider.GetPermissions()))
        {
            PermissionName.EnsureValid(declaration.Name);
            if (!declared.TryAdd(declaration.Name, declaration))
            {
                throw new InvalidOperationException($"Permission '{declaration.Name}' is declared more than once.");
            }
        }

        return declared;
    }
}

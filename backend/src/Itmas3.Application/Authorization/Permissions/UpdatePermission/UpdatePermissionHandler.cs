using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Common.Exceptions;

namespace Itmas3.Application.Authorization.Permissions.UpdatePermission;

public sealed record UpdatePermissionCommand(string Name, string DisplayName, string? Description, string Category);

/// <summary>Use case: edit the presentation metadata of a permission (RF-9). The name is immutable.</summary>
public sealed class UpdatePermissionHandler(IPermissionRepository permissions, IAuthorizationUnitOfWork unitOfWork)
{
    public Task HandleAsync(UpdatePermissionCommand command, CancellationToken cancellationToken)
    {
        var context = new AuditContext(AuditActions.PermissionUpdated, AuditResourceTypes.Permission, command.Name);

        return unitOfWork.ExecuteAsync(context, async (audit, ct) =>
        {
            var permission = await permissions.GetAsync(command.Name, ct)
                ?? throw new NotFoundException($"Permission {command.Name} not found.");

            var before = new { permission.DisplayName, permission.Description, permission.Category };
            permission.UpdateMetadata(command.DisplayName, command.Description ?? string.Empty, command.Category);
            audit.Record(AuditActions.PermissionUpdated, AuditResourceTypes.Permission, permission.Name, before,
                new { permission.DisplayName, permission.Description, permission.Category });
            return true;
        }, cancellationToken);
    }
}

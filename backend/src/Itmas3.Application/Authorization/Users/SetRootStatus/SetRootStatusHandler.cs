using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Common;

namespace Itmas3.Application.Authorization.Users.SetRootStatus;

public sealed record SetRootStatusCommand(Guid UserId, bool IsRoot);

/// <summary>Use case: promote or revoke Root (RF-18). Only Root may do it; at least one active Root must remain (RF-19).</summary>
public sealed class SetRootStatusHandler(IUserRepository users, AdministrationSafeguards safeguards, IAuthorizationUnitOfWork unitOfWork)
{
    public Task HandleAsync(SetRootStatusCommand command, CancellationToken cancellationToken)
    {
        var action = command.IsRoot ? AuditActions.RootGranted : AuditActions.RootRevoked;
        var context = new AuditContext(action, AuditResourceTypes.User, command.UserId.ToString());

        return unitOfWork.ExecuteAsync(context, async (audit, ct) =>
        {
            var actor = await safeguards.LoadActorAsync(ct);
            if (!actor.IsRoot)
            {
                throw new ForbiddenException("Solo un usuario Root puede administrar usuarios Root.");
            }

            var target = await users.GetRequiredAsync(command.UserId, ct);

            if (command.IsRoot && !target.IsActive)
            {
                throw new DomainException("No se puede promover a Root a un usuario desactivado.");
            }

            if (!command.IsRoot && target.IsRoot && target.IsActive && await users.CountActiveRootsAsync(ct) <= 1)
            {
                throw new DomainException("Debe existir al menos un usuario Root activo.");
            }

            if (target.SetRoot(command.IsRoot))
            {
                audit.Record(action, AuditResourceTypes.User, target.Id.ToString(), new { IsRoot = !command.IsRoot }, new { command.IsRoot });
            }

            return true;
        }, cancellationToken);
    }
}

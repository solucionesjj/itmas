using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Domain.Common;

namespace Itmas3.Application.Authorization.Users.SetUserStatus;

public sealed record SetUserStatusCommand(Guid UserId, bool IsActive);

/// <summary>Use case: activate or deactivate a user (RF-4). Deactivation ends the user's sessions (RF-2).</summary>
public sealed class SetUserStatusHandler(IUserRepository users, AdministrationSafeguards safeguards, IAuthorizationUnitOfWork unitOfWork)
{
    public Task HandleAsync(SetUserStatusCommand command, CancellationToken cancellationToken)
    {
        var action = command.IsActive ? AuditActions.UserActivated : AuditActions.UserDeactivated;
        var context = new AuditContext(action, AuditResourceTypes.User, command.UserId.ToString());

        return unitOfWork.ExecuteAsync(context, async (audit, ct) =>
        {
            var actor = await safeguards.LoadActorAsync(ct);
            var target = await users.GetRequiredAsync(command.UserId, ct);

            AdministrationSafeguards.EnsureNotSelf(actor, target.Id);
            AdministrationSafeguards.EnsureTargetNotRoot(actor, target);

            if (!command.IsActive && target.IsRoot && target.IsActive && await users.CountActiveRootsAsync(ct) <= 1)
            {
                throw new DomainException("No se puede desactivar al último usuario Root activo.");
            }

            if (target.SetActive(command.IsActive))
            {
                audit.Record(action, AuditResourceTypes.User, target.Id.ToString(), new { IsActive = !command.IsActive }, new { command.IsActive });
            }

            return true;
        }, cancellationToken);
    }
}

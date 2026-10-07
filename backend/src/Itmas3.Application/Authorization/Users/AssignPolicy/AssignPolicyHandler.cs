using Itmas3.Application.Authorization.Abstractions;

namespace Itmas3.Application.Authorization.Users.AssignPolicy;

public sealed record AssignPolicyCommand(Guid UserId, Guid PolicyId);

/// <summary>Use case: assign a policy to a user (RF-15). Idempotent.</summary>
public sealed class AssignPolicyHandler(
    IUserRepository users,
    IPolicyRepository policies,
    AdministrationSafeguards safeguards,
    IAuthorizationUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task HandleAsync(AssignPolicyCommand command, CancellationToken cancellationToken)
    {
        var context = new AuditContext(AuditActions.UserPolicyAssigned, AuditResourceTypes.User, command.UserId.ToString());

        return unitOfWork.ExecuteAsync(context, async (audit, ct) =>
        {
            var actor = await safeguards.LoadActorAsync(ct);
            var target = await users.GetRequiredAsync(command.UserId, ct);
            var policy = await policies.GetRequiredAsync(command.PolicyId, ct);

            AdministrationSafeguards.EnsureNotSelf(actor, target.Id);
            AdministrationSafeguards.EnsureTargetNotRoot(actor, target);
            AdministrationSafeguards.EnsureHoldsAll(actor, policy.PermissionNames);

            if (target.AssignPolicy(policy.Id, actor.UserId, timeProvider.GetUtcNow()))
            {
                audit.Record(AuditActions.UserPolicyAssigned, AuditResourceTypes.User, target.Id.ToString(), null,
                    new { PolicyId = policy.Id, PolicyName = policy.Name });
            }

            return true;
        }, cancellationToken);
    }
}

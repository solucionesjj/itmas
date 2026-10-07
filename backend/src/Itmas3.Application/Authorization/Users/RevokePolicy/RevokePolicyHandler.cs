using Itmas3.Application.Authorization.Abstractions;

namespace Itmas3.Application.Authorization.Users.RevokePolicy;

public sealed record RevokePolicyCommand(Guid UserId, Guid PolicyId);

/// <summary>Use case: remove a policy from a user (RF-15). Idempotent.</summary>
public sealed class RevokePolicyHandler(
    IUserRepository users,
    IPolicyRepository policies,
    AdministrationSafeguards safeguards,
    IAuthorizationUnitOfWork unitOfWork)
{
    public Task HandleAsync(RevokePolicyCommand command, CancellationToken cancellationToken)
    {
        var context = new AuditContext(AuditActions.UserPolicyRevoked, AuditResourceTypes.User, command.UserId.ToString());

        return unitOfWork.ExecuteAsync(context, async (audit, ct) =>
        {
            var actor = await safeguards.LoadActorAsync(ct);
            var target = await users.GetRequiredAsync(command.UserId, ct);

            AdministrationSafeguards.EnsureNotSelf(actor, target.Id);
            AdministrationSafeguards.EnsureTargetNotRoot(actor, target);

            if (target.RevokePolicy(command.PolicyId))
            {
                var policy = await policies.GetAsync(command.PolicyId, ct);
                audit.Record(AuditActions.UserPolicyRevoked, AuditResourceTypes.User, target.Id.ToString(),
                    new { command.PolicyId, PolicyName = policy?.Name }, null);
            }

            return true;
        }, cancellationToken);
    }
}

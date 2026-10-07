using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Authorization;

namespace Itmas3.Application.Authorization.Policies.CreatePolicy;

public sealed record CreatePolicyCommand(string Name, string? Description, IReadOnlyCollection<string> Permissions);

/// <summary>Use case: create a policy from existing, assignable permissions (RF-10, RF-11, RN-10).</summary>
public sealed class CreatePolicyHandler(
    IPolicyRepository policies,
    IPermissionRepository permissions,
    AdministrationSafeguards safeguards,
    IAuthorizationUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<Guid> HandleAsync(CreatePolicyCommand command, CancellationToken cancellationToken)
    {
        var context = new AuditContext(AuditActions.PolicyCreated, AuditResourceTypes.Policy, null);

        return unitOfWork.ExecuteAsync(context, async (audit, ct) =>
        {
            var actor = await safeguards.LoadActorAsync(ct);
            var requested = await permissions.ResolveAsync(command.Permissions, ct);
            AdministrationSafeguards.EnsureHoldsAll(actor, requested.Select(permission => permission.Name));

            var policy = Policy.Create(command.Name, command.Description ?? string.Empty, requested, timeProvider.GetUtcNow());
            if (await policies.NameExistsAsync(policy.Name, null, ct))
            {
                throw new ConflictException($"Ya existe una política llamada '{policy.Name}'.");
            }

            policies.Add(policy);
            audit.Record(AuditActions.PolicyCreated, AuditResourceTypes.Policy, policy.Id.ToString(), null, PolicyAuditView.Of(policy));
            return policy.Id;
        }, cancellationToken);
    }
}

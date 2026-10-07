using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Common.Exceptions;

namespace Itmas3.Application.Authorization.Policies.UpdatePolicy;

public sealed record UpdatePolicyCommand(
    Guid PolicyId, int ExpectedRevision, string Name, string? Description, bool IsActive, IReadOnlyCollection<string> Permissions);

/// <summary>
/// Use case: modify a policy (RF-10..RF-12). Changes apply to every assigned user on their next request (RF-16).
/// A non-Root administrator may only add permissions they hold, and may only (re)activate policies whose
/// permissions they hold (RN-10).
/// </summary>
public sealed class UpdatePolicyHandler(
    IPolicyRepository policies,
    IPermissionRepository permissions,
    AdministrationSafeguards safeguards,
    IAuthorizationUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<int> HandleAsync(UpdatePolicyCommand command, CancellationToken cancellationToken)
    {
        var context = new AuditContext(AuditActions.PolicyUpdated, AuditResourceTypes.Policy, command.PolicyId.ToString());

        return unitOfWork.ExecuteAsync(context, async (audit, ct) =>
        {
            var actor = await safeguards.LoadActorAsync(ct);
            var policy = await policies.GetRequiredAsync(command.PolicyId, ct);

            if (policy.Revision != command.ExpectedRevision)
            {
                throw new PreconditionFailedException("La política fue modificada por otra persona. Recarga y vuelve a intentarlo.");
            }

            var requested = await permissions.ResolveAsync(command.Permissions, ct);
            var current = policy.PermissionNames.ToHashSet(StringComparer.Ordinal);
            var granted = command.IsActive && !policy.IsActive
                ? requested.Select(permission => permission.Name)
                : requested.Select(permission => permission.Name).Where(name => !current.Contains(name));
            AdministrationSafeguards.EnsureHoldsAll(actor, granted);

            if (await policies.NameExistsAsync(command.Name.Trim(), policy.Id, ct))
            {
                throw new ConflictException($"Ya existe una política llamada '{command.Name.Trim()}'.");
            }

            var before = PolicyAuditView.Of(policy);
            var wasActive = policy.IsActive;
            var onlyStatusChanged = string.Equals(policy.Name, command.Name.Trim(), StringComparison.Ordinal)
                && string.Equals(policy.Description, (command.Description ?? string.Empty).Trim(), StringComparison.Ordinal)
                && current.SetEquals(requested.Select(permission => permission.Name));

            policy.Update(command.Name, command.Description ?? string.Empty, command.IsActive, requested, timeProvider.GetUtcNow());

            var action = onlyStatusChanged && wasActive != policy.IsActive
                ? policy.IsActive ? AuditActions.PolicyActivated : AuditActions.PolicyDeactivated
                : AuditActions.PolicyUpdated;
            audit.Record(action, AuditResourceTypes.Policy, policy.Id.ToString(), before, PolicyAuditView.Of(policy));

            return policy.Revision;
        }, cancellationToken);
    }
}

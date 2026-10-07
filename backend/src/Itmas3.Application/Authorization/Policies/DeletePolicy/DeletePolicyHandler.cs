using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Common.Exceptions;

namespace Itmas3.Application.Authorization.Policies.DeletePolicy;

/// <summary>Use case: delete a policy that is not assigned to any user (RF-10, RF-14).</summary>
public sealed class DeletePolicyHandler(IPolicyRepository policies, IAuthorizationUnitOfWork unitOfWork)
{
    public Task HandleAsync(Guid policyId, CancellationToken cancellationToken)
    {
        var context = new AuditContext(AuditActions.PolicyDeleted, AuditResourceTypes.Policy, policyId.ToString());

        return unitOfWork.ExecuteAsync(context, async (audit, ct) =>
        {
            var policy = await policies.GetRequiredAsync(policyId, ct);

            if (await policies.HasAssignmentsAsync(policy.Id, ct))
            {
                throw new ConflictException("La política está asignada a usuarios. Retírala de todos o desactívala.");
            }

            policies.Remove(policy);
            audit.Record(AuditActions.PolicyDeleted, AuditResourceTypes.Policy, policy.Id.ToString(), PolicyAuditView.Of(policy), null);
            return true;
        }, cancellationToken);
    }
}

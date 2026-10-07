namespace Itmas3.Domain.Authorization;

public sealed class PolicyAssignment
{
    internal PolicyAssignment(Guid policyId, Guid? assignedBy, DateTimeOffset assignedAt)
    {
        PolicyId = policyId;
        AssignedBy = assignedBy;
        AssignedAt = assignedAt;
    }

    public Guid PolicyId { get; private set; }

    public Guid? AssignedBy { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }
}

namespace Itmas3.Infrastructure.Persistence.Records;

/// <summary>
/// Append-only audit record of an authorization-administration operation (SPEC-0001 §14.3).
/// The runtime database role can only INSERT and SELECT this table.
/// </summary>
public sealed class AuditEventRecord
{
    public Guid Id { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public Guid? ActorUserId { get; init; }

    public required string ActorDisplayName { get; init; }

    public required string Action { get; init; }

    public required string ResourceType { get; init; }

    public string? ResourceId { get; init; }

    /// <summary><c>success</c>, <c>rejected</c> (business rule / conflict) or <c>denied</c> (safeguard).</summary>
    public required string Result { get; init; }

    /// <summary>JSON snapshot before the change.</summary>
    public string? Before { get; init; }

    /// <summary>JSON snapshot after the change (or the rejection reason).</summary>
    public string? After { get; init; }

    public string? TraceId { get; init; }
}

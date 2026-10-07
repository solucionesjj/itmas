namespace Itmas3.Infrastructure.Persistence.Records;

/// <summary>
/// Single-row table. <see cref="Version"/> increases with every authorization change (cache invalidation key) and the
/// row is locked by every administrative transaction (serialisation). See ADR-0008.
/// </summary>
public sealed class AuthorizationState
{
    public const short SingletonId = 1;

    public short Id { get; init; }

    public long Version { get; init; }
}

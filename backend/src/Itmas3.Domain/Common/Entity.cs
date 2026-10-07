namespace Itmas3.Domain.Common;

/// <summary>
/// Base type for domain entities. Two entities are equal when they have the same type and identifier.
/// </summary>
/// <typeparam name="TId">Type of the entity identifier.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    protected Entity(TId id)
    {
        Id = id;
    }

    public TId Id { get; private init; }

    public bool Equals(Entity<TId>? other) =>
        other is not null
        && (ReferenceEquals(this, other) || (GetType() == other.GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id)));

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}

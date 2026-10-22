using Itmas3.Domain.Common;

namespace Itmas3.Domain.Agenda;

/// <summary>
/// A free-text label that crosses activity types (SPEC-0006 RF-23). Created the first time it is used; there is no
/// administration (renaming, merging, deleting) in the first version.
/// </summary>
public sealed class AgendaTag : Entity<Guid>
{
    private AgendaTag(Guid id, string name, DateTimeOffset createdAt, Guid createdBy)
        : base(id)
    {
        Name = name;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public static AgendaTag Create(string name, DateTimeOffset now, Guid createdBy) =>
        new(Guid.CreateVersion7(now), TagName.Normalize(name), now, createdBy);
}

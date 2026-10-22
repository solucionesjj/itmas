using Itmas3.Domain.Common;

namespace Itmas3.Domain.Agenda;

/// <summary>
/// A repeating plan (SPEC-0006 RF-4, RN-12). It keeps only its rule, to describe it ("every month, the second Tuesday");
/// the occurrences are <see cref="AgendaEvent"/> rows of their own, each with its own state, responsible and evidence (P-4).
/// </summary>
public sealed class EventSeries : Entity<Guid>
{
    private EventSeries(Guid id, Guid typeId, RecurrencePattern pattern, DateTimeOffset createdAt, Guid createdBy)
        : base(id)
    {
        TypeId = typeId;
        Pattern = pattern;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public Guid TypeId { get; private set; }

    public RecurrencePattern Pattern { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public static EventSeries Create(Guid typeId, RecurrencePattern pattern, DateTimeOffset now, Guid createdBy)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return typeId == Guid.Empty
            ? throw new DomainException("El tipo de actividad es obligatorio.")
            : new EventSeries(Guid.CreateVersion7(now), typeId, pattern, now, createdBy);
    }

    /// <summary>The type of the series follows the common fields of its occurrences (RF-6).</summary>
    public void ChangeType(Guid typeId) =>
        TypeId = typeId == Guid.Empty ? throw new DomainException("El tipo de actividad es obligatorio.") : typeId;

    /// <summary>
    /// Creates the occurrences of the series in Draft state, all with the same <paramref name="details"/> (RF-4). They start
    /// at the pattern's start date and none is earlier (RN-16).
    /// </summary>
    public IReadOnlyList<AgendaEvent> CreateOccurrences(
        EventDetails details,
        IReadOnlyList<EventSchedule> schedules,
        DateTimeOffset now,
        Guid createdBy)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(schedules);
        return [.. schedules.Select(schedule => AgendaEvent.Create(details, schedule, now, createdBy, Id))];
    }
}

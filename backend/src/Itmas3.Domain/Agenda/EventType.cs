using Itmas3.Domain.Common;

namespace Itmas3.Domain.Agenda;

/// <summary>
/// A kind of activity with its defaults (SPEC-0006 RF-1): name, default duration and reminder, and whether it is a change
/// freeze. Types are never deleted, only deactivated (RF-2). The uniqueness of the name (ignoring case) is checked by the
/// use case and enforced by the database.
/// </summary>
public sealed class EventType : Entity<Guid>
{
    public const int MaxNameLength = 80;
    public const int MaxDescriptionLength = 500;

    /// <summary>Longest default duration: the 60 days of an activity (RN-2), in minutes.</summary>
    public const int MaxDurationMinutes = 60 * 24 * 60;

    private EventType(Guid id, string name, DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        IsActive = true;
        Revision = 1;
    }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public int DefaultDurationMinutes { get; private set; }

    public int DefaultReminderMinutes { get; private set; }

    /// <summary>A freeze is a period in which no changes should be scheduled (SPEC-0006 RN-11).</summary>
    public bool IsFreeze { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Incremented on every change; used for optimistic concurrency (ETag).</summary>
    public int Revision { get; private set; }

    public static EventType Create(string? name, string? description, int defaultDurationMinutes, int defaultReminderMinutes, bool isFreeze, DateTimeOffset now)
    {
        var type = new EventType(Guid.CreateVersion7(now), ValidName(name), now);
        type.Apply(description, defaultDurationMinutes, defaultReminderMinutes, isFreeze);
        return type;
    }

    /// <summary>Changes the type and activates or deactivates it (RF-2) as one change: the revision goes up once.</summary>
    public void Update(string? name, string? description, int defaultDurationMinutes, int defaultReminderMinutes, bool isFreeze, bool isActive, DateTimeOffset now)
    {
        Name = ValidName(name);
        Apply(description, defaultDurationMinutes, defaultReminderMinutes, isFreeze);
        IsActive = isActive;
        Touch(now);
    }

    /// <summary>Activates or deactivates the type. An inactive type cannot be assigned to new events (RF-2). Returns false when nothing changes.</summary>
    public bool SetActive(bool isActive, DateTimeOffset now)
    {
        if (IsActive == isActive)
        {
            return false;
        }

        IsActive = isActive;
        Touch(now);
        return true;
    }

    public static string ValidName(string? name) => AgendaText.RequiredLine(name, MaxNameLength, "El nombre del tipo");

    /// <summary>Reminder before the start, in minutes (P-25): from 0 to 4 weeks.</summary>
    public static int ValidReminder(int minutes) =>
        minutes is >= 0 and <= AgendaEvent.MaxReminderMinutes
            ? minutes
            : throw new DomainException($"El recordatorio debe estar entre 0 y {AgendaEvent.MaxReminderMinutes} minutos.");

    private void Apply(string? description, int defaultDurationMinutes, int defaultReminderMinutes, bool isFreeze)
    {
        Description = AgendaText.OptionalText(description, MaxDescriptionLength, "La descripción del tipo");
        DefaultDurationMinutes = defaultDurationMinutes is >= 1 and <= MaxDurationMinutes
            ? defaultDurationMinutes
            : throw new DomainException($"La duración por defecto debe estar entre 1 y {MaxDurationMinutes} minutos.");
        DefaultReminderMinutes = ValidReminder(defaultReminderMinutes);
        IsFreeze = isFreeze;
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Revision++;
    }
}

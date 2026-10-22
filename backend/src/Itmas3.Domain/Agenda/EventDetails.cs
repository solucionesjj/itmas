using System.Text.RegularExpressions;
using Itmas3.Domain.Common;

namespace Itmas3.Domain.Agenda;

/// <summary>
/// The validated descriptive data of an event (SPEC-0006 RF-3): everything except its schedule and its tags. It is built
/// only through <see cref="Create"/>, so an <see cref="AgendaEvent"/> never receives unvalidated text. That the type exists
/// and is active and that the responsible is an active user (RN-1, RN-4) are checked by the use case.
/// </summary>
public sealed class EventDetails
{
    private EventDetails(
        Guid typeId,
        string title,
        string? description,
        Guid responsibleUserId,
        IReadOnlyList<string> participants,
        bool notifyTeam,
        EventImpact impact,
        string? scope,
        string? location,
        int reminderMinutes)
    {
        TypeId = typeId;
        Title = title;
        Description = description;
        ResponsibleUserId = responsibleUserId;
        Participants = participants;
        NotifyTeam = notifyTeam;
        Impact = impact;
        Scope = scope;
        Location = location;
        ReminderMinutes = reminderMinutes;
    }

    public Guid TypeId { get; }

    public string Title { get; }

    public string? Description { get; }

    public Guid ResponsibleUserId { get; }

    /// <summary>Attendee addresses: lowercase, unique, from the allowed domains (RN-5).</summary>
    public IReadOnlyList<string> Participants { get; }

    public bool NotifyTeam { get; }

    public EventImpact Impact { get; }

    public string? Scope { get; }

    public string? Location { get; }

    public int ReminderMinutes { get; }

    /// <summary>Validates and normalizes the data; throws <see cref="DomainException"/> when it breaks RN-1, RN-5 or the limits of RF-3.</summary>
    public static EventDetails Create(
        Guid typeId,
        string? title,
        string? description,
        Guid responsibleUserId,
        IEnumerable<string>? participants,
        IReadOnlyCollection<string> allowedAttendeeDomains,
        bool notifyTeam,
        EventImpact impact,
        string? scope,
        string? location,
        int reminderMinutes)
    {
        if (typeId == Guid.Empty)
        {
            throw new DomainException("El tipo de actividad es obligatorio.");
        }

        if (responsibleUserId == Guid.Empty)
        {
            throw new DomainException("El responsable es obligatorio.");
        }

        if (!Enum.IsDefined(impact))
        {
            throw new DomainException("El impacto no es válido.");
        }

        return new EventDetails(
            typeId,
            AgendaText.RequiredLine(title, AgendaEvent.MaxTitleLength, "El título"),
            AgendaText.OptionalText(description, AgendaEvent.MaxDescriptionLength, "La descripción"),
            responsibleUserId,
            ParticipantList.Normalize(participants, allowedAttendeeDomains),
            notifyTeam,
            impact,
            AgendaText.OptionalLine(scope, AgendaEvent.MaxScopeLength, "El alcance"),
            AgendaText.OptionalLine(location, AgendaEvent.MaxLocationLength, "El lugar"),
            EventType.ValidReminder(reminderMinutes));
    }
}

/// <summary>Participants of an event (SPEC-0006 RN-5): valid, unique addresses of the allowed domains, at most 30.</summary>
public static partial class ParticipantList
{
    public const int MaxParticipants = 30;
    public const int MaxEmailLength = 254;

    /// <summary>Lowercase addresses. Throws when one is malformed, repeated (ignoring case) or outside the allowed domains.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? raw, IReadOnlyCollection<string> allowedDomains)
    {
        ArgumentNullException.ThrowIfNull(allowedDomains);
        var allowed = allowedDomains.Select(domain => domain.Trim().ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in raw ?? [])
        {
            var email = item?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email) || email.Length > MaxEmailLength || !EmailPattern().IsMatch(email))
            {
                throw new DomainException("Un participante no tiene un correo válido.");
            }

            if (!seen.Add(email))
            {
                throw new DomainException("Hay participantes repetidos.");
            }

            if (allowed.Count == 0)
            {
                throw new DomainException("No hay dominios permitidos configurados para los participantes.");
            }

            if (!allowed.Contains(email[(email.LastIndexOf('@') + 1)..]))
            {
                throw new DomainException("Un participante tiene un correo de un dominio no permitido.");
            }

            result.Add(email);
        }

        return result.Count <= MaxParticipants
            ? result
            : throw new DomainException($"Máximo {MaxParticipants} participantes por actividad.");
    }

    // Pragmatic address check, as for device owners: one '@', no spaces or control characters, a dot in the domain.
    [GeneratedRegex(@"^[^@\s\p{Cc}]+@[^@\s\p{Cc}]+\.[^@\s\p{Cc}]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex EmailPattern();
}

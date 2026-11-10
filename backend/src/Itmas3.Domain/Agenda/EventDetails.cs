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

/// <summary>
/// Participants of an event (SPEC-0006 RN-5): valid, unique addresses of the allowed domains, at most 30. Since SPEC-0008 an
/// address must follow the strict rule of <see cref="AgendaEmail"/> (RF-2b), so that it can go in an invitation as it is.
/// </summary>
public static class ParticipantList
{
    public const int MaxParticipants = 30;
    public const int MaxEmailLength = AgendaEmail.MaxLength;

    /// <summary>Longest part of a rejected address quoted back in the error, so the message stays short.</summary>
    private const int MaxQuotedLength = 80;

    /// <summary>Lowercase addresses. Throws when one is malformed, repeated (ignoring case) or outside the allowed domains.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? raw, IReadOnlyCollection<string> allowedDomains)
    {
        ArgumentNullException.ThrowIfNull(allowedDomains);
        var allowed = allowedDomains.Select(domain => domain.Trim().ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in raw ?? [])
        {
            var email = AgendaEmail.Normalize(item);
            if (email is null)
            {
                throw new DomainException(
                    $"El correo «{Quote(item)}» de un participante no es válido: usa solo letras sin tilde, números y . _ + - antes de la @.");
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

    /// <summary>The rejected value as typed, without control characters and cut to <see cref="MaxQuotedLength"/>.</summary>
    private static string Quote(string? value)
    {
        var text = new string([.. (value ?? string.Empty).Trim().Where(character => !char.IsControl(character))]);
        return text.Length <= MaxQuotedLength ? text : string.Concat(text.AsSpan(0, MaxQuotedLength), "…");
    }
}

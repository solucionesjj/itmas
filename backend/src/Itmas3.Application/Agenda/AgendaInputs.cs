using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;

namespace Itmas3.Application.Agenda;

/// <summary>
/// When an event happens, as informed by the caller: local date-times for a timed event, or the first and last day (both
/// inclusive) for an all-day one (RN-2, P-24). A missing end falls back to the default duration of the type; a missing last
/// day, to the first one.
/// </summary>
public sealed record EventTimingInput(bool IsAllDay, DateTime? StartLocal, DateTime? EndLocal, DateOnly? FirstDay, DateOnly? LastDay)
{
    internal EventSchedule ToSchedule(TimeZoneInfo zone, EventType type)
    {
        if (IsAllDay)
        {
            var first = FirstDay ?? throw new DomainException("El primer día es obligatorio.");
            return EventSchedule.AllDay(first, LastDay ?? first, zone);
        }

        var start = StartLocal ?? throw new DomainException("El inicio es obligatorio.");
        return EventSchedule.Timed(start, EndLocal ?? start.AddMinutes(type.DefaultDurationMinutes), zone);
    }
}

/// <summary>The data of an event or series occurrence as informed by the caller (SPEC-0006 RF-3). A null reminder takes the one of the type.</summary>
public sealed record EventInput(
    Guid TypeId,
    string? Title,
    string? Description,
    Guid ResponsibleUserId,
    IReadOnlyList<string>? Participants,
    bool NotifyTeam,
    EventImpact Impact,
    string? Scope,
    string? Location,
    int? ReminderMinutes,
    IReadOnlyList<string>? Tags,
    EventTimingInput Timing);

/// <summary>The common fields of a series (SPEC-0006 RF-6): everything of an event except its dates and "notify the team".</summary>
public sealed record SeriesCommonInput(
    Guid TypeId,
    string? Title,
    string? Description,
    Guid ResponsibleUserId,
    IReadOnlyList<string>? Participants,
    EventImpact Impact,
    string? Scope,
    string? Location,
    int? ReminderMinutes,
    IReadOnlyList<string>? Tags);

/// <summary>An event input after the checks that need the database: its type and responsible exist and are usable.</summary>
public sealed record PreparedEvent(EventType Type, AgendaUser Responsible, EventDetails Details);

/// <summary>
/// Checks shared by the use cases that create or change events: that the type exists and is active (RN-1), that the
/// responsible is an active user (RN-4) and the validation of the data (RN-1, RN-5). A type or a responsible that did not
/// change may be inactive: the rules apply "when creating or changing" them.
/// </summary>
public sealed class AgendaEventPreparer(IEventTypeRepository types, IAgendaDirectory directory, AgendaSettings settings)
{
    public async Task<PreparedEvent> PrepareAsync(
        Guid typeId,
        string? title,
        string? description,
        Guid responsibleUserId,
        IEnumerable<string>? participants,
        bool notifyTeam,
        EventImpact impact,
        string? scope,
        string? location,
        int? reminderMinutes,
        Guid? currentTypeId,
        Guid? currentResponsibleId,
        CancellationToken cancellationToken)
    {
        var type = await types.GetAsync(typeId, cancellationToken) ?? throw new DomainException("El tipo de actividad no existe.");
        if (!type.IsActive && currentTypeId != type.Id)
        {
            throw new DomainException("El tipo de actividad está inactivo y no se puede asignar.");
        }

        var responsible = await directory.GetUserAsync(responsibleUserId, cancellationToken)
            ?? throw new DomainException("El responsable no existe.");
        if (!responsible.IsActive && currentResponsibleId != responsible.Id)
        {
            throw new DomainException("El responsable debe ser un usuario activo de ITMas.");
        }

        var details = EventDetails.Create(
            typeId,
            title,
            description,
            responsibleUserId,
            participants,
            settings.AllowedAttendeeDomains,
            notifyTeam,
            impact,
            scope,
            location,
            reminderMinutes ?? type.DefaultReminderMinutes);
        return new PreparedEvent(type, responsible, details);
    }

    public Task<PreparedEvent> PrepareAsync(EventInput input, Guid? currentTypeId, Guid? currentResponsibleId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return PrepareAsync(
            input.TypeId, input.Title, input.Description, input.ResponsibleUserId, input.Participants, input.NotifyTeam, input.Impact,
            input.Scope, input.Location, input.ReminderMinutes, currentTypeId, currentResponsibleId, cancellationToken);
    }
}

/// <summary>Resolves the tags of an event by name: the existing ones are reused and the missing ones are created (RF-23, P-17).</summary>
public sealed class AgendaTagResolver(IAgendaTagRepository tags)
{
    public async Task<IReadOnlyList<AgendaTag>> ResolveAsync(IEnumerable<string>? names, Guid createdBy, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var normalized = TagName.NormalizeSet(names);
        if (normalized.Count == 0)
        {
            return [];
        }

        var existing = (await tags.FindByKeysAsync([.. normalized.Select(TagName.Key)], cancellationToken))
            .ToDictionary(tag => TagName.Key(tag.Name), StringComparer.Ordinal);
        var result = new List<AgendaTag>(normalized.Count);
        foreach (var name in normalized)
        {
            if (!existing.TryGetValue(TagName.Key(name), out var tag))
            {
                tag = AgendaTag.Create(name, now, createdBy);
                tags.Add(tag);
            }

            result.Add(tag);
        }

        return result;
    }
}

public static class AgendaActor
{
    /// <summary>The id of the signed-in user. Operations of the agenda always come from a provisioned user.</summary>
    public static Guid Of(ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        return currentUser.ApplicationUserId ?? throw new InvalidOperationException("The agenda requires a provisioned user.");
    }
}

/// <summary>Computes the warnings of RN-11 for an activity from the other activities that share its time.</summary>
public sealed class AgendaWarningService(IAgendaEventRepository events)
{
    public async Task<IReadOnlyList<AgendaWarning>> ForAsync(Guid? subjectId, EventSchedule schedule, bool subjectIsFreeze, EventImpact impact, CancellationToken cancellationToken)
    {
        var candidates = await events.FindOverlapCandidatesAsync(schedule.StartUtc, schedule.EndUtc, cancellationToken);
        return AgendaWarnings.Evaluate(subjectId, schedule.StartUtc, schedule.EndUtc, subjectIsFreeze, impact, candidates);
    }

    /// <summary>The warnings of several schedules of one subject (the occurrences of a series), with one query for the whole range.</summary>
    public async Task<IReadOnlyList<IReadOnlyList<AgendaWarning>>> ForManyAsync(
        IReadOnlyList<EventSchedule> schedules, bool subjectIsFreeze, EventImpact impact, CancellationToken cancellationToken)
    {
        if (schedules.Count == 0)
        {
            return [];
        }

        var candidates = await events.FindOverlapCandidatesAsync(
            schedules.Min(schedule => schedule.StartUtc), schedules.Max(schedule => schedule.EndUtc), cancellationToken);
        return [.. schedules.Select(schedule => AgendaWarnings.Evaluate(null, schedule.StartUtc, schedule.EndUtc, subjectIsFreeze, impact, candidates))];
    }
}

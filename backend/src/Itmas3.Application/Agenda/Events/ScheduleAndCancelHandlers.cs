using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;

namespace Itmas3.Application.Agenda.Events;

/// <summary>Command to schedule drafts: up to <see cref="MaxBatch"/> events, with an individual result for each (RF-9, CA-7).</summary>
public sealed record ScheduleEventsCommand(IReadOnlyList<Guid> Ids)
{
    public const int MaxBatch = 50;
}

/// <summary>
/// Use case: Draft → Scheduled for several events at once (SPEC-0006 RF-9). Each event is validated on its own: one with a
/// start in the past is rejected (RN-3) without affecting the others. With Outlook active, each scheduled event is then
/// published, in order, and the batch stops on <c>429</c> (RNF-5); a failure there never undoes the scheduling (RF-20).
/// </summary>
public sealed class ScheduleEventsHandler(
    AgendaSettings settings,
    IAgendaEventRepository events,
    IEventTypeRepository types,
    IAgendaDirectory directory,
    IAgendaUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    AgendaWarningService warnings,
    OutlookPublisher publisher)
{
    public async Task<IReadOnlyList<ScheduleEventResult>> HandleAsync(ScheduleEventsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var ids = command.Ids.Distinct().ToList();
        if (ids.Count is 0 or > ScheduleEventsCommand.MaxBatch)
        {
            throw new ArgumentOutOfRangeException(nameof(command), $"A batch has 1 to {ScheduleEventsCommand.MaxBatch} events.");
        }

        var phase = await unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var now = timeProvider.GetUtcNow();
            var actor = AgendaActor.Of(currentUser);
            var loaded = (await events.GetManyAsync(ids, ct)).ToDictionary(e => e.Id);
            var typeById = await types.GetManyAsync([.. loaded.Values.Select(e => e.TypeId).Distinct()], ct);
            var users = await directory.GetUsersAsync([.. loaded.Values.Select(e => e.ResponsibleUserId).Distinct()], ct);

            var results = new Dictionary<Guid, ScheduleEventResult>();
            var work = new List<OutlookWork>();
            foreach (var id in ids)
            {
                if (!loaded.TryGetValue(id, out var agendaEvent))
                {
                    results[id] = Rejected(id, "La actividad no existe.");
                    continue;
                }

                try
                {
                    agendaEvent.Schedule(now, settings.TimeZone, actor);
                }
                catch (DomainException exception)
                {
                    results[id] = Rejected(id, exception.Message);
                    continue;
                }

                var type = typeById[agendaEvent.TypeId];
                if (settings.OutlookEnabled)
                {
                    agendaEvent.MarkOutlookPending();
                    work.Add(OutlookEventContent.ForPublish(agendaEvent, type, users.GetValueOrDefault(agendaEvent.ResponsibleUserId), settings));
                }
                else
                {
                    agendaEvent.MarkOutlookNotPublished();
                }

                audit.Record(
                    AgendaAuditActions.EventScheduled, AgendaAuditActions.EventResourceType, agendaEvent.Id.ToString(), null, AgendaAuditViews.Of(agendaEvent));
                var eventWarnings = await warnings.ForAsync(agendaEvent.Id, agendaEvent.Timing, type.IsFreeze, agendaEvent.Impact, ct);
                results[id] = new ScheduleEventResult(id, agendaEvent.Status, eventWarnings, OutlookState.Of(agendaEvent), null);
            }

            return (Results: results, Work: work);
        }, cancellationToken);

        var states = await publisher.RunAsync(phase.Work, cancellationToken);
        return [.. ids.Select(id => states.TryGetValue(id, out var state) ? phase.Results[id] with { Outlook = state } : phase.Results[id])];
    }

    private static ScheduleEventResult Rejected(Guid id, string error) =>
        new(id, null, [], new OutlookState(OutlookStatus.NotPublished), error);
}

public sealed record CancelEventCommand(Guid EventId, string? Reason);

/// <summary>
/// Use case: cancel a Scheduled event with a reason (SPEC-0006 RF-10, RN-8). If it was published, the cancellation is sent
/// to Outlook with the reason after saving; a failure there leaves the event cancelled and its Outlook state in error.
/// </summary>
public sealed class CancelEventHandler(
    AgendaSettings settings,
    IAgendaEventRepository events,
    IAgendaUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    OutlookPublisher publisher)
{
    public async Task<AgendaCommandResult> HandleAsync(CancelEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var saved = await unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var agendaEvent = await events.GetAsync(command.EventId, ct) ?? throw new NotFoundException("Agenda event not found.");
            var before = AgendaAuditViews.OfCancellation(agendaEvent);
            agendaEvent.Cancel(command.Reason, timeProvider.GetUtcNow(), AgendaActor.Of(currentUser));

            OutlookWork? work = null;
            if (agendaEvent.OutlookEventId is not null && settings.OutlookEnabled)
            {
                agendaEvent.MarkOutlookPending();
                work = OutlookEventContent.ForCancel(agendaEvent);
            }

            audit.Record(
                AgendaAuditActions.EventCancelled, AgendaAuditActions.EventResourceType, agendaEvent.Id.ToString(), before, AgendaAuditViews.OfCancellation(agendaEvent));
            return (Event: agendaEvent, Work: work);
        }, cancellationToken);

        var outlook = OutlookState.Of(saved.Event);
        if (saved.Work is not null)
        {
            outlook = (await publisher.RunAsync([saved.Work], cancellationToken))[saved.Event.Id];
        }

        return new AgendaCommandResult(saved.Event.Id, saved.Event.Revision, saved.Event.Status, [], outlook);
    }
}

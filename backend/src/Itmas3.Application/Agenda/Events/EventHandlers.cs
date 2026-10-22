using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;

namespace Itmas3.Application.Agenda.Events;

/// <summary>
/// Use case: create an event in Draft state (SPEC-0006 RF-3). The start cannot be in the past (RN-16). Returns the
/// warnings of RN-11, which never prevent saving.
/// </summary>
public sealed class CreateEventHandler(
    AgendaSettings settings,
    IAgendaEventRepository events,
    IAgendaUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    AgendaEventPreparer preparer,
    AgendaWarningService warnings,
    AgendaTagResolver resolver)
{
    public Task<AgendaCommandResult> HandleAsync(EventInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var now = timeProvider.GetUtcNow();
            var actor = AgendaActor.Of(currentUser);
            var prepared = await preparer.PrepareAsync(input, null, null, ct);
            var schedule = input.Timing.ToSchedule(settings.TimeZone, prepared.Type);
            if (schedule.IsInPast(now, settings.TimeZone))
            {
                throw new DomainException("El inicio no puede estar en el pasado.");
            }

            var tagList = await resolver.ResolveAsync(input.Tags, actor, now, ct);
            var agendaEvent = AgendaEvent.Create(prepared.Details, schedule, now, actor);
            agendaEvent.ReplaceTags([.. tagList]);
            events.Add(agendaEvent);

            audit.Record(AgendaAuditActions.EventCreated, AgendaAuditActions.EventResourceType, agendaEvent.Id.ToString(), null, AgendaAuditViews.Of(agendaEvent));
            var eventWarnings = await warnings.ForAsync(agendaEvent.Id, schedule, prepared.Type.IsFreeze, agendaEvent.Impact, ct);
            return new AgendaCommandResult(agendaEvent.Id, agendaEvent.Revision, agendaEvent.Status, eventWarnings, OutlookState.Of(agendaEvent));
        }, cancellationToken);
    }
}

/// <summary>
/// Use case: modify a Draft or Scheduled event (RF-5) with optimistic concurrency (RNF-3). The type and the responsible only
/// have to be active when they change. A Scheduled event is updated in Outlook after saving (RF-17); a failure there never
/// undoes the change (RF-20). Tags, when given, replace the current ones.
/// </summary>
public sealed class UpdateEventHandler(
    AgendaSettings settings,
    IAgendaEventRepository events,
    IAgendaUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    AgendaEventPreparer preparer,
    AgendaWarningService warnings,
    AgendaTagResolver resolver,
    OutlookPublisher publisher,
    AgendaNotificationGuard guard)
{
    public async Task<AgendaCommandResult> HandleAsync(UpdateEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The unit of work may run the closure again (race of a new tag): the budget is spent only once.
        var notifyChecked = false;
        var saved = await unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var now = timeProvider.GetUtcNow();
            var actor = AgendaActor.Of(currentUser);
            var agendaEvent = await events.GetAsync(command.EventId, ct) ?? throw new NotFoundException("Agenda event not found.");
            if (agendaEvent.Revision != command.ExpectedRevision)
            {
                throw new PreconditionFailedException("Otro usuario modificó esta actividad. Recarga los datos e inténtalo de nuevo.");
            }

            if (agendaEvent.Status == EventStatus.Scheduled && !notifyChecked)
            {
                await guard.EnsureCanNotifyAsync(ct);
                notifyChecked = true;
            }

            var before = AgendaAuditViews.Of(agendaEvent);
            var prepared = await preparer.PrepareAsync(command.Input, agendaEvent.TypeId, agendaEvent.ResponsibleUserId, ct);
            var schedule = command.Input.Timing.ToSchedule(settings.TimeZone, prepared.Type);
            agendaEvent.Update(prepared.Details, schedule, now, settings.TimeZone, actor);
            if (command.Input.Tags is not null)
            {
                agendaEvent.ReplaceTags([.. await resolver.ResolveAsync(command.Input.Tags, actor, now, ct)]);
            }

            var work = PrepareOutlook(agendaEvent, prepared.Type, prepared.Responsible);
            audit.Record(AgendaAuditActions.EventUpdated, AgendaAuditActions.EventResourceType, agendaEvent.Id.ToString(), before, AgendaAuditViews.Of(agendaEvent));
            var eventWarnings = await warnings.ForAsync(agendaEvent.Id, schedule, prepared.Type.IsFreeze, agendaEvent.Impact, ct);
            return (Event: agendaEvent, Warnings: eventWarnings, Work: work);
        }, cancellationToken);

        var outlook = OutlookState.Of(saved.Event);
        if (saved.Work is not null)
        {
            var states = await publisher.RunAsync([saved.Work], cancellationToken);
            outlook = states[saved.Event.Id];
        }

        return new AgendaCommandResult(saved.Event.Id, saved.Event.Revision, saved.Event.Status, saved.Warnings, outlook);
    }

    // A scheduled event tells Outlook about the change; a draft, or Outlook being off, changes nothing there. While its creation is
    // still on its way (Pending and no Outlook id yet) a second creation is not sent: it would carry the same transactionId and Graph
    // may answer with the event already created, without the change. It stays Pending and "Sincronizar de nuevo" sends it.
    private OutlookWork? PrepareOutlook(AgendaEvent agendaEvent, EventType type, AgendaUser responsible)
    {
        if (agendaEvent.Status != EventStatus.Scheduled || !settings.OutlookEnabled)
        {
            return null;
        }

        if (agendaEvent.OutlookEventId is null && agendaEvent.OutlookStatus == OutlookStatus.Pending)
        {
            return null;
        }

        agendaEvent.MarkOutlookPending();
        return OutlookEventContent.ForPublish(agendaEvent, type, responsible, settings);
    }
}

public sealed record UpdateEventCommand(Guid EventId, int ExpectedRevision, EventInput Input);

/// <summary>Use case: delete a Draft event (RF-7). Scheduled events are cancelled and closed ones are kept (RN-13).</summary>
public sealed class DeleteDraftEventHandler(IAgendaEventRepository events, IAgendaUnitOfWork unitOfWork)
{
    public Task HandleAsync(Guid eventId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var agendaEvent = await events.GetAsync(eventId, ct) ?? throw new NotFoundException("Agenda event not found.");
            agendaEvent.EnsureCanBeDeleted();

            events.Remove(agendaEvent);
            audit.Record(AgendaAuditActions.EventDeleted, AgendaAuditActions.EventResourceType, agendaEvent.Id.ToString(), AgendaAuditViews.Of(agendaEvent), null);
            return true;
        }, cancellationToken);
}

using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;

namespace Itmas3.Application.Agenda.Outlook;

/// <summary>
/// Use case: "Sync again" with Outlook (SPEC-0006 RF-19). It retries one event, or every Scheduled event that has not started
/// and is Pending, in Error or Not published (up to <see cref="MaxBatch"/>). It is idempotent: an event is created with its id as
/// transaction id and then updated with the saved id, so nothing is duplicated; if the Outlook event was deleted there, the
/// adapter creates it again (RN-14). A Cancelled event whose cancellation was not confirmed can be retried on its own.
/// With Outlook off nothing is called (RF-15).
/// </summary>
public sealed class SyncWithOutlookHandler(
    AgendaSettings settings,
    IAgendaEventRepository events,
    IEventTypeRepository types,
    IAgendaDirectory directory,
    IAgendaUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    OutlookPublisher publisher)
{
    public const int MaxBatch = 50;

    public async Task<SyncResult> SyncOneAsync(Guid eventId, CancellationToken cancellationToken)
    {
        if (!settings.OutlookEnabled)
        {
            // Nothing is called and nothing is stored: the event simply is not published (CA-17).
            var agendaEvent = await events.GetAsync(eventId, cancellationToken) ?? throw new NotFoundException("Agenda event not found.");
            return new SyncResult(agendaEvent.Id, new OutlookState(OutlookStatus.NotPublished, OutlookErrorCodes.NotConfigured));
        }

        var work = await unitOfWork.ExecuteAsync(async (_, ct) =>
        {
            var agendaEvent = await events.GetAsync(eventId, ct) ?? throw new NotFoundException("Agenda event not found.");
            if (agendaEvent.Status == EventStatus.Cancelled)
            {
                if (!agendaEvent.NeedsOutlookCancellation)
                {
                    throw new DomainException("La cancelación de esta actividad ya está confirmada en Outlook o nunca se publicó.");
                }

                agendaEvent.MarkOutlookPending();
                return OutlookEventContent.ForCancel(agendaEvent);
            }

            if (agendaEvent.Status != EventStatus.Scheduled)
            {
                throw new DomainException("Solo se pueden sincronizar con Outlook las actividades programadas.");
            }

            agendaEvent.MarkOutlookPending();
            return (await BuildPublishWorkAsync([agendaEvent], ct))[0];
        }, cancellationToken);

        var states = await publisher.RunAsync([work], cancellationToken);
        return new SyncResult(eventId, states[eventId]);
    }

    public async Task<IReadOnlyList<SyncResult>> SyncPendingAsync(CancellationToken cancellationToken)
    {
        if (!settings.OutlookEnabled)
        {
            return [];
        }

        var work = await unitOfWork.ExecuteAsync(async (_, ct) =>
        {
            var pending = await events.FindNeedingOutlookSyncAsync(timeProvider.GetUtcNow(), MaxBatch, ct);
            foreach (var agendaEvent in pending)
            {
                agendaEvent.MarkOutlookPending();
            }

            return await BuildPublishWorkAsync(pending, ct);
        }, cancellationToken);

        var states = await publisher.RunAsync(work, cancellationToken);
        return [.. work.Select(item => new SyncResult(item.EventId, states[item.EventId]))];
    }

    private async Task<IReadOnlyList<OutlookWork>> BuildPublishWorkAsync(IReadOnlyList<AgendaEvent> agendaEvents, CancellationToken cancellationToken)
    {
        if (agendaEvents.Count == 0)
        {
            return [];
        }

        var typeById = await types.GetManyAsync([.. agendaEvents.Select(e => e.TypeId).Distinct()], cancellationToken);
        var users = await directory.GetUsersAsync([.. agendaEvents.Select(e => e.ResponsibleUserId).Distinct()], cancellationToken);
        return [.. agendaEvents.Select(e => OutlookEventContent.ForPublish(e, typeById[e.TypeId], users.GetValueOrDefault(e.ResponsibleUserId), settings))];
    }
}

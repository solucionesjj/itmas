using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;

namespace Itmas3.Application.Agenda.Series;

/// <summary>The repetition rule as informed by the caller (SPEC-0006 RF-4): every N days, weeks, months or years, limited by a date, a count or both.</summary>
public sealed record SeriesRuleInput(RecurrenceUnit Unit, int Interval, RecurrenceAnchor? Anchor, DateOnly? EndsOn, int? Count);

/// <summary>Creates a series from the data of its first occurrence and a rule. With <see cref="DryRun"/> nothing is saved (RF-4 preview).</summary>
public sealed record CreateSeriesCommand(EventInput Event, SeriesRuleInput Rule, bool DryRun);

public sealed record UpdateSeriesCommand(Guid SeriesId, SeriesCommonInput Common);

/// <summary>
/// Use case: create a series and its occurrences in Draft state, or preview them (SPEC-0006 RF-4, RN-6, RN-12, RN-16, CA-3).
/// The preview runs every check and returns the dates and warnings the series would have, without saving anything: not
/// even the tags that would be created.
/// </summary>
public sealed class CreateSeriesHandler(
    AgendaSettings settings,
    AgendaEventPreparer preparer,
    AgendaWarningService warnings,
    AgendaTagResolver resolver,
    IAgendaEventRepository events,
    IEventSeriesRepository seriesRepository,
    IAgendaUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<CreateSeriesResult> HandleAsync(CreateSeriesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Event);
        ArgumentNullException.ThrowIfNull(command.Rule);
        if (command.DryRun)
        {
            var plan = await PlanAsync(command, cancellationToken);
            TagName.NormalizeSet(command.Event.Tags);
            return new CreateSeriesResult(plan.Previews, null, []);
        }

        return await unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var now = timeProvider.GetUtcNow();
            var actor = AgendaActor.Of(currentUser);
            var plan = await PlanAsync(command, ct);
            var tags = await resolver.ResolveAsync(command.Event.Tags, actor, now, ct);

            var series = EventSeries.Create(plan.Details.TypeId, plan.Pattern, now, actor);
            var occurrences = series.CreateOccurrences(plan.Details, [.. plan.Previews.Select(preview => preview.Schedule)], now, actor);
            foreach (var occurrence in occurrences)
            {
                occurrence.ReplaceTags([.. tags]);
            }

            seriesRepository.Add(series);
            events.AddRange(occurrences);
            audit.Record(AgendaAuditActions.SeriesCreated, AgendaAuditActions.SeriesResourceType, series.Id.ToString(), null, AgendaAuditViews.OfSeries(series, occurrences));
            return new CreateSeriesResult(plan.Previews, series.Id, [.. occurrences.Select(occurrence => occurrence.Id)]);
        }, cancellationToken);
    }

    private async Task<SeriesPlan> PlanAsync(CreateSeriesCommand command, CancellationToken cancellationToken)
    {
        var prepared = await preparer.PrepareAsync(command.Event, null, null, cancellationToken);
        var template = command.Event.Timing.ToSchedule(settings.TimeZone, prepared.Type);
        var rule = command.Rule;
        var pattern = RecurrencePattern.Create(rule.Unit, rule.Interval, rule.Anchor, template.FirstDay, rule.EndsOn, rule.Count);
        var schedules = RecurrenceGenerator.Generate(pattern, template, settings.TimeZone, timeProvider.GetUtcNow());
        var occurrenceWarnings = await warnings.ForManyAsync(schedules, prepared.Type.IsFreeze, prepared.Details.Impact, cancellationToken);
        var previews = schedules.Select((schedule, index) => new SeriesOccurrencePreview(schedule, occurrenceWarnings[index])).ToList();
        return new SeriesPlan(pattern, prepared.Details, previews);
    }

    private sealed record SeriesPlan(RecurrencePattern Pattern, EventDetails Details, IReadOnlyList<SeriesOccurrencePreview> Previews);
}

/// <summary>
/// Use case: change the common fields of a series (SPEC-0006 RF-6, CA-5, CA-28). Only occurrences that have not started and
/// are in Draft or Scheduled state change; the dates never do, and completed or cancelled ones are not touched. Tags, when
/// given, replace the ones of those occurrences. Scheduled occurrences are updated in Outlook after saving.
/// </summary>
public sealed class UpdateSeriesHandler(
    AgendaSettings settings,
    AgendaEventPreparer preparer,
    AgendaTagResolver resolver,
    IAgendaEventRepository events,
    IEventSeriesRepository seriesRepository,
    IAgendaUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    OutlookPublisher publisher,
    AgendaNotificationGuard guard)
{
    public async Task<UpdateSeriesResult> HandleAsync(UpdateSeriesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Common);
        var common = command.Common;

        // The unit of work may run the closure again (race of a new tag): the budget is spent only once.
        var notifyChecked = false;
        var saved = await unitOfWork.ExecuteAsync(async (audit, ct) =>
        {
            var now = timeProvider.GetUtcNow();
            var actor = AgendaActor.Of(currentUser);
            var series = await seriesRepository.GetAsync(command.SeriesId, ct) ?? throw new NotFoundException("Agenda series not found.");

            // The values apply to many events as new ones, so the type and the responsible must be active.
            var prepared = await preparer.PrepareAsync(
                common.TypeId, common.Title, common.Description, common.ResponsibleUserId, common.Participants, notifyTeam: true,
                common.Impact, common.Scope, common.Location, common.ReminderMinutes, null, null, ct);
            var tags = common.Tags is null ? null : await resolver.ResolveAsync(common.Tags, actor, now, ct);

            var updated = new List<AgendaEvent>();
            var before = new List<object>();
            var work = new List<OutlookWork>();
            foreach (var occurrence in await events.GetBySeriesAsync(series.Id, ct))
            {
                var previous = AgendaAuditViews.Of(occurrence);
                var wasScheduled = occurrence.Status == EventStatus.Scheduled;
                if (!occurrence.TryApplyCommonFields(prepared.Details, now, actor))
                {
                    continue;
                }

                if (wasScheduled && !notifyChecked)
                {
                    await guard.EnsureCanNotifyAsync(ct);
                    notifyChecked = true;
                }

                before.Add(new { occurrence.Id, Values = previous });

                if (tags is not null)
                {
                    occurrence.ReplaceTags([.. tags]);
                }

                // While its creation is on its way (Pending and no Outlook id yet) a second creation is not sent: see UpdateEventHandler.
                if (occurrence.Status == EventStatus.Scheduled && settings.OutlookEnabled
                    && !(occurrence.OutlookEventId is null && occurrence.OutlookStatus == OutlookStatus.Pending))
                {
                    occurrence.MarkOutlookPending();
                    work.Add(OutlookEventContent.ForPublish(occurrence, prepared.Type, prepared.Responsible, settings));
                }

                updated.Add(occurrence);
            }

            series.ChangeType(prepared.Details.TypeId);
            audit.Record(
                AgendaAuditActions.SeriesUpdated, AgendaAuditActions.SeriesResourceType, series.Id.ToString(), new { Events = before },
                new
                {
                    series.TypeId,
                    prepared.Details.Title,
                    prepared.Details.ResponsibleUserId,
                    Impact = AgendaCodes.ToCode(prepared.Details.Impact),
                    prepared.Details.ReminderMinutes,
                    Tags = tags?.Select(tag => tag.Name).ToList(),
                    UpdatedEventIds = updated.Select(occurrence => occurrence.Id).ToList(),
                });
            return (Updated: updated, Work: work);
        }, cancellationToken);

        await publisher.RunAsync(saved.Work, cancellationToken);
        return new UpdateSeriesResult(saved.Updated.Count, [.. saved.Updated.Select(occurrence => occurrence.Id)]);
    }
}

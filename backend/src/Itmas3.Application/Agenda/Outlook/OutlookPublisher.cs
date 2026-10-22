using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Agenda;

namespace Itmas3.Application.Agenda.Outlook;

public enum OutlookOperationKind
{
    Publish,
    Cancel,
}

/// <summary>
/// One call to make to Outlook after the change has been saved (SPEC-0006 §10.3). <see cref="Revision"/> is the revision the
/// call carries, so that an event that changes meanwhile is not reported as up to date.
/// </summary>
public sealed record OutlookWork(
    Guid EventId,
    int Revision,
    OutlookOperationKind Kind,
    string? OutlookEventId,
    OutlookEventData? Data,
    string? CancelReason);

/// <summary>Builds what is sent to Outlook (SPEC-0006 RF-16): subject, plain-text body, category, reminder and invitees.</summary>
internal static class OutlookEventContent
{
    public static OutlookWork ForPublish(AgendaEvent agendaEvent, EventType type, AgendaUser? responsible, AgendaSettings settings)
    {
        ArgumentNullException.ThrowIfNull(agendaEvent);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(settings);

        var required = new List<string>();
        // The invitations go only to the allowed domains (RN-5, Q-4), the responsible included: a guest account or an external
        // sign-in name is not invited (the activity keeps its responsible in ITMas).
        if (responsible?.Email is { Length: > 0 } responsibleEmail && IsAllowed(responsibleEmail, settings))
        {
            required.Add(responsibleEmail);
        }

        required.AddRange(agendaEvent.Participants);
        var requiredAddresses = Distinct(required, []);
        var optional = agendaEvent.NotifyTeam && !string.IsNullOrWhiteSpace(settings.TeamAddress)
            ? Distinct([settings.TeamAddress], requiredAddresses)
            : [];

        var data = new OutlookEventData(
            agendaEvent.Id,
            $"[{type.Name}] {agendaEvent.Title}",
            Body(agendaEvent, settings),
            agendaEvent.StartsAtLocal,
            agendaEvent.EndsAtLocal,
            agendaEvent.TimeZoneId,
            agendaEvent.IsAllDay,
            agendaEvent.Location,
            agendaEvent.ReminderMinutes,
            type.Name,
            requiredAddresses,
            optional);
        return new OutlookWork(agendaEvent.Id, agendaEvent.Revision, OutlookOperationKind.Publish, agendaEvent.OutlookEventId, data, null);
    }

    public static OutlookWork ForCancel(AgendaEvent agendaEvent) =>
        new(agendaEvent.Id, agendaEvent.Revision, OutlookOperationKind.Cancel, agendaEvent.OutlookEventId, null, agendaEvent.CancelReason);

    /// <summary>Plain text: description, scope, impact and the link to the activity in ITMas (RF-16). Nothing is HTML.</summary>
    internal static string Body(AgendaEvent agendaEvent, AgendaSettings settings)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(agendaEvent.Description))
        {
            lines.Add(agendaEvent.Description);
        }

        if (!string.IsNullOrWhiteSpace(agendaEvent.Scope))
        {
            lines.Add($"Alcance: {agendaEvent.Scope}");
        }

        lines.Add($"Impacto: {ImpactLabel(agendaEvent.Impact)}");
        if (!string.IsNullOrWhiteSpace(settings.PublicUrl))
        {
            lines.Add($"Más información: {settings.PublicUrl.TrimEnd('/')}/agenda/events/{agendaEvent.Id}");
        }

        return string.Join("\n\n", lines);
    }

    /// <summary>The Spanish label of the impact, shared by the Outlook body and the calendar export (RF-16, RF-21).</summary>
    internal static string ImpactLabel(EventImpact impact) => impact switch
    {
        EventImpact.Degradation => "Degradación del servicio",
        EventImpact.Outage => "Indisponibilidad",
        _ => "Ninguno",
    };

    private static bool IsAllowed(string address, AgendaSettings settings)
    {
        var at = address.LastIndexOf('@');
        return at > 0
            && settings.AllowedAttendeeDomains.Contains(address[(at + 1)..].Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> Distinct(IEnumerable<string> addresses, IReadOnlyCollection<string> exclude)
    {
        var seen = new HashSet<string>(exclude, StringComparer.OrdinalIgnoreCase);
        return [.. addresses.Where(address => !string.IsNullOrWhiteSpace(address) && seen.Add(address.Trim())).Select(address => address.Trim())];
    }
}

/// <summary>
/// Calls Outlook after a change has been saved and saves the result in a second write (SPEC-0006 §10.3, RF-20). An event
/// whose call does not complete stays Pending: "Sync again" resolves it. On <c>429</c> the batch stops and the rest stay
/// Pending (RNF-5). A failure of Outlook never fails the operation that asked for it.
/// </summary>
public sealed class OutlookPublisher(
    IOutlookCalendarGateway gateway,
    IAgendaUnitOfWork unitOfWork,
    IAgendaEventRepository events,
    TimeProvider timeProvider)
{
    /// <summary>Executes the work in order and returns the Outlook state of each event.</summary>
    public async Task<IReadOnlyDictionary<Guid, OutlookState>> RunAsync(IReadOnlyList<OutlookWork> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        var states = new Dictionary<Guid, OutlookState>();
        var throttled = false;
        foreach (var item in work)
        {
            var operation = item.Kind == OutlookOperationKind.Cancel ? "cancel" : "publish";
            if (throttled)
            {
                states[item.EventId] = new OutlookState(OutlookStatus.Pending);
                AgendaMetrics.RecordOutlookOperation(operation, "not-attempted");
                continue;
            }

            var result = await CallAsync(item, cancellationToken);
            throttled = result.Throttled;
            AgendaMetrics.RecordOutlookOperation(operation, result.Succeeded ? "success" : result.ErrorCode!);
            var (state, followUp) = await RecordAsync(item, result, cancellationToken);
            states[item.EventId] = state;

            // The event was cancelled while Outlook was creating it: the cancellation had no Outlook id to send, so it is sent now
            // with the id just received, instead of leaving the invitation alive.
            if (followUp is not null)
            {
                var cancelled = await CallAsync(followUp, cancellationToken);
                throttled = cancelled.Throttled;
                AgendaMetrics.RecordOutlookOperation("cancel", cancelled.Succeeded ? "success" : cancelled.ErrorCode!);
                (states[item.EventId], _) = await RecordAsync(followUp, cancelled, cancellationToken);
            }
        }

        return states;
    }

    private async Task<OutlookGatewayResult> CallAsync(OutlookWork item, CancellationToken cancellationToken)
    {
        try
        {
            if (item.Kind == OutlookOperationKind.Cancel)
            {
                // Only events that were published have something to cancel; the use cases never ask for anything else.
                return item.OutlookEventId is { } id
                    ? Normalize(await gateway.CancelAsync(id, item.CancelReason ?? string.Empty, cancellationToken))
                    : OutlookGatewayResult.Failure(OutlookErrorCodes.Rejected);
            }

            var result = Normalize(await gateway.PublishAsync(item.Data!, item.OutlookEventId, cancellationToken));
            return result.Succeeded && string.IsNullOrWhiteSpace(result.OutlookEventId) && string.IsNullOrWhiteSpace(item.OutlookEventId)
                ? OutlookGatewayResult.Failure(OutlookErrorCodes.Rejected)
                : result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OutlookGatewayResult.Failure(OutlookErrorCodes.Timeout);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The port promises not to throw for expected failures; anything else is reported with a closed code, never its detail.
            return OutlookGatewayResult.Failure(OutlookErrorCodes.Unavailable);
        }
    }

    private static OutlookGatewayResult Normalize(OutlookGatewayResult result) =>
        result.Succeeded ? result : result with { ErrorCode = OutlookErrorCodes.Normalize(result.ErrorCode) };

    /// <summary>
    /// The second write: the outcome of the call. A concurrent change to the event leaves it Pending. When a creation finds the event
    /// already cancelled, the cancellation to send with the new Outlook id is returned as well.
    /// </summary>
    private async Task<(OutlookState State, OutlookWork? FollowUp)> RecordAsync(
        OutlookWork item, OutlookGatewayResult result, CancellationToken cancellationToken)
    {
        try
        {
            return await unitOfWork.ExecuteAsync<(OutlookState, OutlookWork?)>(async (audit, ct) =>
            {
                var agendaEvent = await events.GetAsync(item.EventId, ct);
                if (agendaEvent is null)
                {
                    return (new OutlookState(OutlookStatus.NotPublished), null);
                }

                var now = timeProvider.GetUtcNow();
                var operation = item.Kind == OutlookOperationKind.Cancel ? "cancel" : "publish";
                if (result.Succeeded)
                {
                    agendaEvent.MarkOutlookPublished(
                        FirstNonEmpty(result.OutlookEventId, item.OutlookEventId)!,
                        result.WebLink ?? agendaEvent.OutlookWebLink,
                        item.Revision,
                        now);
                    audit.Record(
                        AgendaAuditActions.EventOutlookPublished, AgendaAuditActions.EventResourceType, agendaEvent.Id.ToString(), null,
                        AgendaAuditViews.OfOutlook(agendaEvent.Id, operation, item.Revision, null));
                }
                else
                {
                    agendaEvent.MarkOutlookFailed(result.ErrorCode!, now);
                    audit.Record(
                        AgendaAuditActions.EventOutlookFailed, AgendaAuditActions.EventResourceType, agendaEvent.Id.ToString(), null,
                        AgendaAuditViews.OfOutlook(agendaEvent.Id, operation, item.Revision, result.ErrorCode));
                }

                OutlookWork? followUp = null;
                if (result.Succeeded && item.Kind == OutlookOperationKind.Publish && agendaEvent.NeedsOutlookCancellation)
                {
                    agendaEvent.MarkOutlookPending();
                    followUp = OutlookEventContent.ForCancel(agendaEvent);
                }

                return (OutlookState.Of(agendaEvent), followUp);
            }, cancellationToken);
        }
        catch (ConflictException)
        {
            return (new OutlookState(OutlookStatus.Pending), null);
        }
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

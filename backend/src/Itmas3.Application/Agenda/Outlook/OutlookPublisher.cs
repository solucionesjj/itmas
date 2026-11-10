using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Authorization.Abstractions;
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
/// call carries, so that an event that changes meanwhile is not reported as up to date. <see cref="AddedRecipients"/> are the
/// addresses the SMTP write-ahead of this call added (SPEC-0008 RF-4): only those may be forgotten when the server rejects them.
/// </summary>
public sealed record OutlookWork(
    Guid EventId,
    int Revision,
    OutlookOperationKind Kind,
    string? OutlookEventId,
    OutlookEventData? Data,
    string? CancelReason)
{
    public IReadOnlyList<string> AddedRecipients { get; init; } = [];
}

/// <summary>Builds what is sent to Outlook (SPEC-0006 RF-16): subject, plain-text body, category, reminder and invitees.</summary>
internal static class OutlookEventContent
{
    /// <summary>
    /// The update (or creation) of a scheduled event. With SMTP it also writes ahead (SPEC-0008 RF-4): the <c>UID</c> and the
    /// recipients of the <c>REQUEST</c> are stored in the event, in the same write as the change, and whoever may hold the meeting and is
    /// no longer invited goes in <see cref="OutlookEventData.RemovedRecipients"/>. Call it inside the unit of work that saves the change.
    /// </summary>
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

        var data = Data(agendaEvent, type, settings, requiredAddresses, optional) with { Sequence = (2 * agendaEvent.Revision) + 1 };
        if (!settings.UsesSmtp)
        {
            return new OutlookWork(agendaEvent.Id, agendaEvent.Revision, OutlookOperationKind.Publish, agendaEvent.OutlookEventId, data, null);
        }

        var uid = InvitationWriter.UidOf(agendaEvent.Id);
        if (agendaEvent.OutlookEventId is { } existing && !string.Equals(existing, uid, StringComparison.Ordinal))
        {
            // Published with the other transport (ADR-0023 decision 10): nothing is written ahead, and the adapter answers
            // outlook_rejected for this event alone, so neither the change nor the rest of a batch is blocked.
            return new OutlookWork(agendaEvent.Id, agendaEvent.Revision, OutlookOperationKind.Publish, existing, data, null);
        }

        // The recipients the writer will use (RF-2b), so that nobody is stored who will never get anything.
        var (recipients, _) = OutlookRecipients.Normalize([.. requiredAddresses, .. optional]);
        var removed = OutlookRecipients.Removed(agendaEvent.OutlookSentRecipients, recipients, settings);
        var added = agendaEvent.RecordInvitationRecipients(uid, recipients);
        return new OutlookWork(agendaEvent.Id, agendaEvent.Revision, OutlookOperationKind.Publish, uid, data with { RemovedRecipients = removed }, null)
        {
            AddedRecipients = added,
        };
    }

    /// <summary>
    /// The cancellation of a cancelled event (RF-10). With SMTP the meeting is repeated with <c>METHOD:CANCEL</c> to whoever may hold it,
    /// filtered with the domains allowed now (SPEC-0008 RF-3). <paramref name="type"/> gives the subject; without it the title is used.
    /// </summary>
    public static OutlookWork ForCancel(AgendaEvent agendaEvent, EventType? type, AgendaSettings settings)
    {
        ArgumentNullException.ThrowIfNull(agendaEvent);
        ArgumentNullException.ThrowIfNull(settings);
        var recipients = settings.UsesSmtp ? OutlookRecipients.Reachable(agendaEvent.OutlookSentRecipients, settings) : [];
        var data = Data(agendaEvent, type, settings, recipients, []) with
        {
            Sequence = (2 * agendaEvent.Revision) + 1,
            CancelReason = agendaEvent.CancelReason,
        };
        return new(agendaEvent.Id, agendaEvent.Revision, OutlookOperationKind.Cancel, agendaEvent.OutlookEventId, data, agendaEvent.CancelReason);
    }

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

    private static OutlookEventData Data(
        AgendaEvent agendaEvent, EventType? type, AgendaSettings settings, IReadOnlyList<string> required, IReadOnlyList<string> optional) =>
        new(
            agendaEvent.Id,
            type is null ? agendaEvent.Title : $"[{type.Name}] {agendaEvent.Title}",
            Body(agendaEvent, settings),
            agendaEvent.StartsAtLocal,
            agendaEvent.EndsAtLocal,
            agendaEvent.TimeZoneId,
            agendaEvent.IsAllDay,
            agendaEvent.Location,
            agendaEvent.ReminderMinutes,
            type?.Name ?? string.Empty,
            required,
            optional)
        {
            StartUtc = agendaEvent.StartsAtUtc,
            EndUtc = agendaEvent.EndsAtUtc,
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
/// Calls Outlook after a change has been saved and saves the result in a second write (SPEC-0006 §10.3, RF-20). An event whose call
/// does not complete stays Pending: "Sync again" resolves it. When the gateway asks to stop (429, or SPEC-0008 RF-6) the batch stops
/// and the rest stay Pending (RNF-5). A failure of Outlook never fails the operation that asked for it.
/// <para>
/// With SMTP (SPEC-0008 RF-6): the batch has its own deadline, 45 s from the start of the request, and its own cancellation, so a
/// client that goes away does not cut a send half way; the second write only removes recipients and is retried once on a conflict;
/// there is no follow-up cancellation, because the write-ahead already stored the <c>UID</c> that the cancellation needs.
/// </para>
/// </summary>
public sealed class OutlookPublisher(
    IOutlookCalendarGateway gateway,
    IAgendaUnitOfWork unitOfWork,
    IAgendaEventRepository events,
    AgendaSettings settings,
    TimeProvider timeProvider)
{
    /// <summary>Time budget of an SMTP batch from the start of the request, below the 60 s of nginx (SPEC-0008 RF-6).</summary>
    public static readonly TimeSpan SmtpBudget = TimeSpan.FromSeconds(45);

    /// <summary>Extra time the own cancellation of an SMTP batch leaves after the budget, for the message in flight and the second write.</summary>
    public static readonly TimeSpan SmtpGrace = TimeSpan.FromSeconds(12);

    /// <summary>Executes the work in order and returns the Outlook state of each event. The budget starts now.</summary>
    public Task<IReadOnlyDictionary<Guid, OutlookState>> RunAsync(IReadOnlyList<OutlookWork> work, CancellationToken cancellationToken) =>
        RunAsync(work, timeProvider.GetUtcNow(), cancellationToken);

    /// <summary>Executes the work in order. <paramref name="startedAt"/> is when the request began (the SMTP budget counts from it).</summary>
    public async Task<IReadOnlyDictionary<Guid, OutlookState>> RunAsync(
        IReadOnlyList<OutlookWork> work, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        var states = new Dictionary<Guid, OutlookState>();
        if (work.Count == 0)
        {
            return states;
        }

        var smtp = settings.UsesSmtp;
        var deadline = startedAt + SmtpBudget;
        using var own = smtp ? new CancellationTokenSource(Positive(deadline + SmtpGrace - timeProvider.GetUtcNow()), timeProvider) : null;
        var token = own?.Token ?? cancellationToken;

        var stopped = false;
        foreach (var item in work)
        {
            var operation = item.Kind == OutlookOperationKind.Cancel ? "cancel" : "publish";
            if (stopped || (smtp && timeProvider.GetUtcNow() >= deadline))
            {
                states[item.EventId] = new OutlookState(OutlookStatus.Pending);
                AgendaMetrics.RecordOutlookOperation(operation, "not-attempted");
                continue;
            }

            var call = smtp && item.Data is not null ? item with { Data = item.Data with { Deadline = deadline } } : item;
            var result = await CallAsync(call, token, ownsToken: smtp);
            stopped = result.StopsBatch;
            AgendaMetrics.RecordOutlookOperation(operation, result.NotAttempted ? "not-attempted" : result.Succeeded ? "success" : result.ErrorCode!);
            var (state, followUp) = await RecordAsync(item, result, smtp, token);
            states[item.EventId] = state;

            // Graph only: the event was cancelled while Outlook was creating it. The cancellation had no Outlook id to send, so it is
            // sent now with the id just received, instead of leaving the invitation alive.
            if (followUp is not null && !stopped)
            {
                var cancelled = await CallAsync(followUp, token, ownsToken: false);
                stopped = cancelled.StopsBatch;
                AgendaMetrics.RecordOutlookOperation("cancel", cancelled.Succeeded ? "success" : cancelled.ErrorCode!);
                (states[item.EventId], _) = await RecordAsync(followUp, cancelled, smtp, token);
            }
        }

        return states;
    }

    private static TimeSpan Positive(TimeSpan value) => value > TimeSpan.Zero ? value : TimeSpan.FromMilliseconds(1);

    private async Task<OutlookGatewayResult> CallAsync(OutlookWork item, CancellationToken cancellationToken, bool ownsToken)
    {
        try
        {
            if (item.Kind == OutlookOperationKind.Cancel)
            {
                // Only events that were published have something to cancel; the use cases never ask for anything else.
                return item.OutlookEventId is { } id
                    ? Normalize(await gateway.CancelAsync(id, item.CancelReason ?? string.Empty, item.Data, cancellationToken))
                    : OutlookGatewayResult.Failure(OutlookErrorCodes.OutlookRejected);
            }

            var result = Normalize(await gateway.PublishAsync(item.Data!, item.OutlookEventId, cancellationToken));
            return result.Succeeded && string.IsNullOrWhiteSpace(result.OutlookEventId) && string.IsNullOrWhiteSpace(item.OutlookEventId)
                ? OutlookGatewayResult.Failure(OutlookErrorCodes.OutlookRejected)
                : result;
        }
        catch (OperationCanceledException) when (ownsToken || !cancellationToken.IsCancellationRequested)
        {
            return OutlookGatewayResult.Failure(OutlookErrorCodes.OutlookTimeout);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The port promises not to throw for expected failures; anything else is reported with a closed code, never its detail.
            return OutlookGatewayResult.Failure(OutlookErrorCodes.OutlookUnavailable);
        }
    }

    private static OutlookGatewayResult Normalize(OutlookGatewayResult result) =>
        result.ErrorCode is null ? result : result with { ErrorCode = OutlookErrorCodes.Normalize(result.ErrorCode) };

    /// <summary>
    /// The second write: the outcome of the call. With Graph a concurrent change leaves the event Pending; with SMTP the write is tried
    /// once more, because it only removes recipients. With Graph, when a creation finds the event already cancelled, the cancellation to
    /// send with the new Outlook id is returned as well.
    /// </summary>
    private async Task<(OutlookState State, OutlookWork? FollowUp)> RecordAsync(
        OutlookWork item, OutlookGatewayResult result, bool smtp, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await unitOfWork.ExecuteAsync<(OutlookState, OutlookWork?)>(
                    (audit, ct) => RecordOnceAsync(item, result, smtp, audit, ct), cancellationToken);
            }
            catch (ConflictException) when (smtp && attempt == 1)
            {
                // Removing recipients is idempotent: read the event again and apply the outcome to it.
            }
            catch (ConflictException)
            {
                return (new OutlookState(OutlookStatus.Pending), null);
            }
            catch (OperationCanceledException) when (smtp)
            {
                // The own deadline of the batch ran out during the second write: the change is already saved and the event stays
                // Pending. At worst a removal is not recorded and the person gets one cancellation too many later.
                return (new OutlookState(OutlookStatus.Pending), null);
            }
        }
    }

    private async Task<(OutlookState, OutlookWork?)> RecordOnceAsync(
        OutlookWork item, OutlookGatewayResult result, bool smtp, AuditTrail audit, CancellationToken cancellationToken)
    {
        var agendaEvent = await events.GetAsync(item.EventId, cancellationToken);
        if (agendaEvent is null)
        {
            return (new OutlookState(OutlookStatus.NotPublished), null);
        }

        var now = timeProvider.GetUtcNow();
        var operation = item.Kind == OutlookOperationKind.Cancel ? "cancel" : "publish";
        if (smtp)
        {
            // Only removes (SPEC-0008 RF-4): whoever got the cancellation, and whoever this send added and the server refused for good.
            var rejected = result.RejectedRecipients.Intersect(item.AddedRecipients, StringComparer.OrdinalIgnoreCase);
            agendaEvent.ForgetInvitationRecipients(item.Revision, [.. result.ConfirmedRemovals, .. rejected]);
        }

        if (result.NotAttempted)
        {
            agendaEvent.MarkOutlookPending(item.Revision);
            return (OutlookState.Of(agendaEvent), null);
        }

        var counts = (result.RecipientCount, Removed: result.ConfirmedRemovals.Count, result.OmittedCount);
        if (result.Succeeded)
        {
            agendaEvent.MarkOutlookPublished(
                FirstNonEmpty(result.OutlookEventId, item.OutlookEventId)!,
                result.WebLink ?? agendaEvent.OutlookWebLink,
                item.Revision,
                now);
            audit.Record(
                AgendaAuditActions.EventOutlookPublished, AgendaAuditActions.EventResourceType, agendaEvent.Id.ToString(), null,
                AgendaAuditViews.OfOutlook(agendaEvent.Id, operation, item.Revision, null, smtp ? counts : null));
        }
        else
        {
            agendaEvent.MarkOutlookFailed(result.ErrorCode!, now, item.Revision);
            audit.Record(
                AgendaAuditActions.EventOutlookFailed, AgendaAuditActions.EventResourceType, agendaEvent.Id.ToString(), null,
                AgendaAuditViews.OfOutlook(agendaEvent.Id, operation, item.Revision, result.ErrorCode, smtp ? counts : null));
        }

        OutlookWork? followUp = null;
        if (!smtp && result.Succeeded && item.Kind == OutlookOperationKind.Publish && agendaEvent.NeedsOutlookCancellation)
        {
            agendaEvent.MarkOutlookPending();
            followUp = OutlookEventContent.ForCancel(agendaEvent, null, settings);
        }

        return (OutlookState.Of(agendaEvent), followUp);
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

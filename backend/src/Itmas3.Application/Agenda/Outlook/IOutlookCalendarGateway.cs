namespace Itmas3.Application.Agenda.Outlook;

/// <summary>
/// What is sent to Outlook for an event (SPEC-0006 RF-16). Text is plain: HTML typed by a person is delivered literally and never
/// interpreted (RNF-7). Local times and the all-day end follow SPEC-0006 P-24 (exclusive end).
/// <para>
/// The init-only members were added by SPEC-0008 for the SMTP transport (ADR-0023 decisions 4 to 7); the Graph adapter ignores them.
/// </para>
/// </summary>
public sealed record OutlookEventData(
    Guid EventId,
    string Subject,
    string BodyText,
    DateTime StartLocal,
    DateTime EndLocal,
    string TimeZoneId,
    bool IsAllDay,
    string? Location,
    int ReminderMinutes,
    string Category,
    IReadOnlyList<string> RequiredAttendees,
    IReadOnlyList<string> OptionalAttendees)
{
    /// <summary>The <c>SEQUENCE</c> of the invitation: <c>2·revision + 1</c> for an update or a cancellation of the activity.</summary>
    public int Sequence { get; init; }

    /// <summary>The start as an instant, so the SMTP adapter does not repeat the conversion of the domain.</summary>
    public DateTimeOffset StartUtc { get; init; }

    /// <summary>The end as an instant.</summary>
    public DateTimeOffset EndUtc { get; init; }

    /// <summary>
    /// Who may hold the meeting and is no longer invited (SPEC-0008 RF-3b): they get one <c>CANCEL</c> with <c>SEQUENCE = Sequence − 1</c>
    /// before the update. Never shares an address with the attendees.
    /// </summary>
    public IReadOnlyList<string> RemovedRecipients { get; init; } = [];

    /// <summary>The cancellation reason, for a cancellation of the activity.</summary>
    public string? CancelReason { get; init; }

    /// <summary>
    /// The instant after which no new message may start (SPEC-0008 RF-6). The adapter starts a message only if the connection (when
    /// none is open), the message and a margin fit before it; otherwise it answers <see cref="OutlookGatewayResult.NotAttempted"/>.
    /// </summary>
    public DateTimeOffset? Deadline { get; init; }
}

/// <summary>
/// Result of an operation with Outlook. <see cref="ErrorCode"/> is null on success and otherwise one of
/// <see cref="OutlookErrorCodes"/>: the raw response of the service is never part of it (RNF-8). <see cref="Throttled"/> and
/// <see cref="StopBatch"/> ask the caller to stop the batch (RNF-5, SPEC-0008 RF-6).
/// </summary>
public sealed record OutlookGatewayResult(string? OutlookEventId, string? WebLink, string? ErrorCode, bool Throttled = false)
{
    public bool Succeeded => ErrorCode is null && !NotAttempted;

    /// <summary>Stop the batch: the rest stays Pending (authentication or sender refused, limits of the server).</summary>
    public bool StopBatch { get; init; }

    /// <summary>
    /// Nothing was sent, or only part of it (the cancellation to whoever was removed but not the update), with no error: the event
    /// stays Pending and "Sincronizar de nuevo" sends it (SPEC-0008 RF-5).
    /// </summary>
    public bool NotAttempted { get; init; }

    /// <summary>Addresses whose <c>CANCEL</c> the server accepted (SPEC-0008 RF-4).</summary>
    public IReadOnlyList<string> ConfirmedRemovals { get; init; } = [];

    /// <summary>Addresses the server rejected for good at <c>RCPT</c> (SPEC-0008 RF-4).</summary>
    public IReadOnlyList<string> RejectedRecipients { get; init; } = [];

    /// <summary>How many recipients went in the messages and how many were left out because their address broke the rule (audit counts).</summary>
    public int RecipientCount { get; init; }

    public int OmittedCount { get; init; }

    public static OutlookGatewayResult Success(string outlookEventId, string? webLink) => new(outlookEventId, webLink, null);

    public static OutlookGatewayResult Failure(string errorCode, bool throttled = false) => new(null, null, errorCode, throttled);

    /// <summary>Nothing (or only part) was sent; the event stays Pending.</summary>
    public static OutlookGatewayResult Postponed(string? outlookEventId, bool stopBatch = false) =>
        new(outlookEventId, null, null) { NotAttempted = true, StopBatch = stopBatch };

    /// <summary>Whether the caller must not start anything else in this batch.</summary>
    public bool StopsBatch => Throttled || StopBatch;
}

/// <summary>
/// Port to Outlook (ADR-0016, ADR-0023). Implementations never throw for an expected failure of the service (unauthorized,
/// throttled, unavailable, timeout…): they return a failed <see cref="OutlookGatewayResult"/> with a normalized code. They never call
/// anything but the configured service.
/// </summary>
public interface IOutlookCalendarGateway
{
    /// <summary>
    /// Creates or updates the event for its attendees. With Graph: created with the id of the agenda event as transaction id, or
    /// updated when <paramref name="outlookEventId"/> is given, and created again if it no longer exists (RF-19, RN-14). With SMTP: an
    /// invitation <c>REQUEST</c>, after a <c>CANCEL</c> to <see cref="OutlookEventData.RemovedRecipients"/> (SPEC-0008 RF-2, RF-3b).
    /// </summary>
    Task<OutlookGatewayResult> PublishAsync(OutlookEventData data, string? outlookEventId, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels the event, sending the cancellation with the reason to the invitees (RF-10). An event that no longer exists counts as
    /// cancelled. <paramref name="data"/> carries the meeting being cancelled, which an iCalendar <c>CANCEL</c> repeats; Graph ignores it.
    /// </summary>
    Task<OutlookGatewayResult> CancelAsync(string outlookEventId, string reason, OutlookEventData? data, CancellationToken cancellationToken);
}

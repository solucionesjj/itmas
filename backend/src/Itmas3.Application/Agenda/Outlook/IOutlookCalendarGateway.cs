namespace Itmas3.Application.Agenda.Outlook;

/// <summary>
/// What is sent to the shared Outlook calendar for an event (SPEC-0006 RF-16). Text is plain: HTML typed by a person is
/// delivered literally and never interpreted (RNF-7). Local times and the all-day end follow SPEC-0006 P-24 (exclusive end).
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
    IReadOnlyList<string> OptionalAttendees);

/// <summary>
/// Result of an operation with Outlook. <see cref="ErrorCode"/> is null on success and otherwise one of
/// <see cref="OutlookErrorCodes"/>: the raw response of Graph is never part of it (RNF-8). <see cref="Throttled"/> asks the
/// caller to stop the batch (HTTP 429, RNF-5).
/// </summary>
public sealed record OutlookGatewayResult(string? OutlookEventId, string? WebLink, string? ErrorCode, bool Throttled = false)
{
    public bool Succeeded => ErrorCode is null;

    public static OutlookGatewayResult Success(string outlookEventId, string? webLink) => new(outlookEventId, webLink, null);

    public static OutlookGatewayResult Failure(string errorCode, bool throttled = false) => new(null, null, errorCode, throttled);
}

/// <summary>
/// Port to the shared Outlook calendar (ADR-0016). Implementations never throw for an expected failure of the service
/// (unauthorized, throttled, unavailable, timeout…): they return a failed <see cref="OutlookGatewayResult"/> with a
/// normalized code. They never call anything but the configured calendar.
/// </summary>
public interface IOutlookCalendarGateway
{
    /// <summary>
    /// Creates the event (with the id of the agenda event as transaction id, so a retry never duplicates it) or, when
    /// <paramref name="outlookEventId"/> is given, updates it. If that event no longer exists there it is created again
    /// (RF-19, RN-14).
    /// </summary>
    Task<OutlookGatewayResult> PublishAsync(OutlookEventData data, string? outlookEventId, CancellationToken cancellationToken);

    /// <summary>Cancels the event, sending the cancellation with the reason to the invitees (RF-10). An event that no longer exists counts as cancelled.</summary>
    Task<OutlookGatewayResult> CancelAsync(string outlookEventId, string reason, CancellationToken cancellationToken);
}

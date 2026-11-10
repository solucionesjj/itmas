using Itmas3.Domain.Agenda;

namespace Itmas3.Application.Agenda;

/// <summary>Wire names (JSON, query strings and database codes) of the agenda enumerations (SPEC-0006 §12.1, §13).</summary>
public static class AgendaCodes
{
    public static string ToCode(EventStatus status) => status switch
    {
        EventStatus.Scheduled => "scheduled",
        EventStatus.Completed => "completed",
        EventStatus.Cancelled => "cancelled",
        _ => "draft",
    };

    public static EventStatus? ParseStatus(string? code) => code switch
    {
        "draft" => EventStatus.Draft,
        "scheduled" => EventStatus.Scheduled,
        "completed" => EventStatus.Completed,
        "cancelled" => EventStatus.Cancelled,
        _ => null,
    };

    public static string ToCode(EventImpact impact) => impact switch
    {
        EventImpact.Degradation => "degradation",
        EventImpact.Outage => "outage",
        _ => "none",
    };

    public static EventImpact? ParseImpact(string? code) => code switch
    {
        "none" => EventImpact.None,
        "degradation" => EventImpact.Degradation,
        "outage" => EventImpact.Outage,
        _ => null,
    };

    public static string ToCode(CompletionResult result) => result switch
    {
        CompletionResult.Partial => "partial",
        CompletionResult.Failed => "failed",
        CompletionResult.NotPerformed => "not-performed",
        _ => "succeeded",
    };

    public static CompletionResult? ParseResult(string? code) => code switch
    {
        "succeeded" => CompletionResult.Succeeded,
        "partial" => CompletionResult.Partial,
        "failed" => CompletionResult.Failed,
        "not-performed" => CompletionResult.NotPerformed,
        _ => null,
    };

    public static string ToCode(OutlookStatus status) => status switch
    {
        OutlookStatus.Pending => "pending",
        OutlookStatus.Published => "published",
        OutlookStatus.Error => "error",
        _ => "not-published",
    };

    public static OutlookStatus? ParseOutlookStatus(string? code) => code switch
    {
        "not-published" => OutlookStatus.NotPublished,
        "pending" => OutlookStatus.Pending,
        "published" => OutlookStatus.Published,
        "error" => OutlookStatus.Error,
        _ => null,
    };

    public static string ToCode(RecurrenceUnit unit) => unit switch
    {
        RecurrenceUnit.Week => "week",
        RecurrenceUnit.Month => "month",
        RecurrenceUnit.Year => "year",
        _ => "day",
    };

    public static RecurrenceUnit? ParseUnit(string? code) => code switch
    {
        "day" => RecurrenceUnit.Day,
        "week" => RecurrenceUnit.Week,
        "month" => RecurrenceUnit.Month,
        "year" => RecurrenceUnit.Year,
        _ => null,
    };

    public static string ToCode(RecurrenceAnchorKind kind) => kind == RecurrenceAnchorKind.NthWeekday ? "nth-weekday" : "day-of-month";

    public static RecurrenceAnchorKind? ParseAnchorKind(string? code) => code switch
    {
        "day-of-month" => RecurrenceAnchorKind.DayOfMonth,
        "nth-weekday" => RecurrenceAnchorKind.NthWeekday,
        _ => null,
    };

    public static string ToCode(AgendaWarningKind kind) => kind == AgendaWarningKind.FreezeOverlap ? "freeze-overlap" : "impact-overlap";
}

/// <summary>
/// The closed set of normalized Outlook error codes (SPEC-0006 §12.1, SPEC-0008 §11). The raw answer of Graph or of the SMTP
/// server is never kept or shown (RNF-8, SPEC-0008 RNF-2).
/// </summary>
public static class OutlookErrorCodes
{
    // Microsoft Graph (ADR-0016).
    public const string Unauthorized = "graph_unauthorized";
    public const string Forbidden = "graph_forbidden";
    public const string NotFound = "graph_not_found";
    public const string Throttled = "graph_throttled";
    public const string Unavailable = "graph_unavailable";
    public const string Timeout = "graph_timeout";
    public const string Rejected = "graph_rejected";

    // SMTP of Microsoft 365 (ADR-0023 decision 8).
    public const string SmtpAuthFailed = "smtp_auth_failed";
    public const string SmtpBasicAuthDisabled = "smtp_basic_auth_disabled";
    public const string SmtpSenderRejected = "smtp_sender_rejected";
    public const string SmtpRejected = "smtp_rejected";
    public const string SmtpThrottled = "smtp_throttled";
    public const string SmtpUnavailable = "smtp_unavailable";
    public const string SmtpTimeout = "smtp_timeout";

    // Neutral fallbacks of the publisher, whatever the transport: an unexpected exception, a timeout or an answer without an id.
    public const string OutlookUnavailable = "outlook_unavailable";
    public const string OutlookTimeout = "outlook_timeout";
    public const string OutlookRejected = "outlook_rejected";

    public const string NotConfigured = "not_configured";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Unauthorized, Forbidden, NotFound, Throttled, Unavailable, Timeout, Rejected,
        SmtpAuthFailed, SmtpBasicAuthDisabled, SmtpSenderRejected, SmtpRejected, SmtpThrottled, SmtpUnavailable, SmtpTimeout,
        OutlookUnavailable, OutlookTimeout, OutlookRejected,
        NotConfigured,
    };

    /// <summary>The code itself when it belongs to the closed set, otherwise <see cref="OutlookUnavailable"/>.</summary>
    public static string Normalize(string? code) => code is not null && All.Contains(code) ? code : OutlookUnavailable;
}

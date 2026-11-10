namespace Itmas3.Application.Agenda;

/// <summary>How the agenda reaches Outlook (SPEC-0008 RF-1): invitations by SMTP (ADR-0023, the default) or Microsoft Graph (ADR-0016).</summary>
public enum OutlookTransport
{
    Smtp,
    Graph,
}

/// <summary>Deployment settings of the agenda (SPEC-0006 §12.3), built and validated by the composition root. None is a secret.</summary>
public sealed class AgendaSettings
{
    /// <summary>The single time zone of the organization (RNF-4).</summary>
    public required TimeZoneInfo TimeZone { get; init; }

    /// <summary>Domains attendees may belong to, lowercase (RN-5). Empty: no attendee can be added.</summary>
    public IReadOnlyList<string> AllowedAttendeeDomains { get; init; } = [];

    /// <summary>Public URL of ITMas, for the link in the body of the invitation (P-27).</summary>
    public string? PublicUrl { get; init; }

    /// <summary>
    /// Whether publishing to Outlook is configured on (<c>AGENDA_OUTLOOK_ENABLED</c>). What the use cases and the screens follow is
    /// <see cref="IsOutlookActive"/>, which also honours the expiry of the SMTP exception.
    /// </summary>
    public bool OutlookEnabled { get; init; }

    /// <summary>The transport used when Outlook is on.</summary>
    public OutlookTransport Transport { get; init; } = OutlookTransport.Smtp;

    /// <summary>
    /// SMTP only (SPEC-0008 RF-7, ADR-0023 decision 11): the instant the accepted exception of the constitution §3.8 expires. From then
    /// on nothing is sent and the screens show "Outlook no configurado" until a new express acceptance renews it in code. Null: no expiry.
    /// </summary>
    public DateTimeOffset? SmtpAcceptedUntil { get; init; }

    /// <summary>Distribution list of the team, invited as optional when "notify the team" is on. Null: the option is not offered.</summary>
    public string? TeamAddress { get; init; }

    /// <summary>The address that sends the SMTP invitations and organizes the meetings. Null with Graph or Outlook off.</summary>
    public string? SenderAddress { get; init; }

    /// <summary>Whether the SMTP transport keeps the list of who may hold the meeting (SPEC-0008 RF-4).</summary>
    public bool UsesSmtp => Transport == OutlookTransport.Smtp;

    /// <summary>
    /// Whether anything is published at <paramref name="now"/>: Outlook is on and, with SMTP, the acceptance has not expired. When
    /// false nothing external is called (SPEC-0006 RF-15) and nothing is written ahead (SPEC-0008 RF-4).
    /// </summary>
    public bool IsOutlookActive(DateTimeOffset now) =>
        OutlookEnabled && (Transport != OutlookTransport.Smtp || SmtpAcceptedUntil is not { } until || now < until);
}

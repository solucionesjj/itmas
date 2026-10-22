namespace Itmas3.Application.Agenda;

/// <summary>Deployment settings of the agenda (SPEC-0006 §12.3), built and validated by the composition root. None is a secret.</summary>
public sealed class AgendaSettings
{
    /// <summary>The single time zone of the organization (RNF-4).</summary>
    public required TimeZoneInfo TimeZone { get; init; }

    /// <summary>Domains attendees may belong to, lowercase (RN-5). Empty: no attendee can be added.</summary>
    public IReadOnlyList<string> AllowedAttendeeDomains { get; init; } = [];

    /// <summary>Public URL of ITMas, for the link in the body of the invitation (P-27).</summary>
    public string? PublicUrl { get; init; }

    /// <summary>Whether events are published to the shared Outlook calendar. When false nothing external is called (RF-15).</summary>
    public bool OutlookEnabled { get; init; }

    /// <summary>Distribution list of the team, invited as optional when "notify the team" is on. Null: the option is not offered.</summary>
    public string? TeamAddress { get; init; }
}

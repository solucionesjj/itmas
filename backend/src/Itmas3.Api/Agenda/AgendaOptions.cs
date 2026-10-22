using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Itmas3.Api.Agenda;

/// <summary>
/// Configuration section <c>Agenda</c> (SPEC-0006 §12.3). Environment variables use <c>Agenda__*</c> (for example
/// <c>Agenda__Outlook__Enabled</c>). Only <see cref="AgendaOutlookOptions.ClientSecret"/> is a secret: it is never logged or
/// returned, and the start-up validation messages name the settings that are wrong, never their values.
/// </summary>
internal sealed class AgendaOptions
{
    public const string SectionName = "Agenda";

    /// <summary>IANA time zone of the organization (RNF-4). Cannot be changed once there are events (P-28).</summary>
    [Required]
    public string TimeZone { get; set; } = "America/Bogota";

    /// <summary>Domains attendees may belong to, comma separated. Empty: the domain of the Outlook mailbox (Q-4).</summary>
    public string AllowedAttendeeDomains { get; set; } = string.Empty;

    /// <summary>Public URL of ITMas, for the link in the invitation. Required (absolute https) when Outlook is enabled (P-27).</summary>
    public string? PublicUrl { get; set; }

    /// <summary>
    /// Operations per hour that notify through Outlook (schedule, cancel, sync and changing what is already scheduled), counted
    /// together for the whole API process and only after authorization (P-26, §14.1).
    /// </summary>
    [Range(1, 100_000)]
    public int PublishPermitLimitPerHour { get; set; } = 60;

    public AgendaOutlookOptions Outlook { get; set; } = new();
}

/// <summary>Publication to the shared Outlook calendar (ADR-0016). Disabled by default: with it off nothing external is called.</summary>
internal sealed class AgendaOutlookOptions
{
    public bool Enabled { get; set; }

    public string? TenantId { get; set; }

    public string? ClientId { get; set; }

    /// <summary>SECRET. Credential of the Entra ID application of Graph. Only in the environment of the <c>api</c> container.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Address of the shared mailbox whose calendar is used (Q-2).</summary>
    public string? Mailbox { get; set; }

    /// <summary>Optional distribution list of the team, invited as optional when "notify the team" is on (Q-2).</summary>
    public string? TeamAddress { get; set; }
}

/// <summary>
/// Start-up validation of <see cref="AgendaOptions"/> (CA-18): with Outlook on, every Outlook setting is required and the
/// API does not start without them. Messages list the names of the settings that fail, never their values.
/// </summary>
internal sealed partial class AgendaOptionsValidator : IValidateOptions<AgendaOptions>
{
    public ValidateOptionsResult Validate(string? name, AgendaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (!TryFindZone(options.TimeZone, out _))
        {
            failures.Add("Agenda:TimeZone is not a time zone known to this environment.");
        }

        var domains = ParseDomains(options.AllowedAttendeeDomains);
        if (domains.Any(domain => !DomainPattern().IsMatch(domain)))
        {
            failures.Add("Agenda:AllowedAttendeeDomains has an entry that is not a domain name.");
        }

        if (!string.IsNullOrWhiteSpace(options.PublicUrl) && !IsAbsoluteHttp(options.PublicUrl))
        {
            failures.Add("Agenda:PublicUrl must be an absolute http or https URL.");
        }

        if (options.Outlook.Enabled)
        {
            ValidateOutlook(options, domains, failures);
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    internal static bool TryFindZone(string? id, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
            return true;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    /// <summary>Lowercase domains from a comma, semicolon or space separated list.</summary>
    internal static IReadOnlyList<string> ParseDomains(string? value) =>
        [.. (value ?? string.Empty)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(domain => domain.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)];

    internal static string? DomainOf(string? address)
    {
        var at = address?.LastIndexOf('@') ?? -1;
        return at > 0 && at < address!.Length - 1 ? address[(at + 1)..].ToLowerInvariant() : null;
    }

    private static void ValidateOutlook(AgendaOptions options, IReadOnlyList<string> domains, List<string> failures)
    {
        var outlook = options.Outlook;
        foreach (var (setting, value) in new[]
        {
            ("Agenda:Outlook:TenantId", outlook.TenantId),
            ("Agenda:Outlook:ClientId", outlook.ClientId),
            ("Agenda:Outlook:ClientSecret", outlook.ClientSecret),
            ("Agenda:Outlook:Mailbox", outlook.Mailbox),
        })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                failures.Add($"{setting} is required when Agenda:Outlook:Enabled is true.");
            }
        }

        if (string.IsNullOrWhiteSpace(options.PublicUrl) || !IsAbsoluteHttp(options.PublicUrl)
            || !Uri.TryCreate(options.PublicUrl, UriKind.Absolute, out var publicUrl) || publicUrl.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add("Agenda:PublicUrl must be an absolute https URL when Agenda:Outlook:Enabled is true.");
        }

        var mailboxDomain = DomainOf(outlook.Mailbox);
        if (!string.IsNullOrWhiteSpace(outlook.Mailbox) && (mailboxDomain is null || !DomainPattern().IsMatch(mailboxDomain)))
        {
            failures.Add("Agenda:Outlook:Mailbox must be an e-mail address.");
        }

        if (!string.IsNullOrWhiteSpace(outlook.TeamAddress))
        {
            var allowed = domains.Count > 0 ? domains : mailboxDomain is null ? [] : [mailboxDomain];
            var teamDomain = DomainOf(outlook.TeamAddress);
            if (teamDomain is null || !allowed.Contains(teamDomain, StringComparer.Ordinal))
            {
                failures.Add("Agenda:Outlook:TeamAddress must be an e-mail address of an allowed domain.");
            }
        }
    }

    private static bool IsAbsoluteHttp(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    [GeneratedRegex(@"^(?=.{1,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DomainPattern();
}

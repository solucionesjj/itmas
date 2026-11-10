using Itmas3.Domain.Agenda;

namespace Itmas3.Application.Agenda.Outlook;

/// <summary>
/// The recipient rules of the SMTP transport (SPEC-0008 RF-2b, RF-3, RF-3b). Pure functions: they only compare normalized addresses
/// (<see cref="AgendaEmail"/>), without telling case apart.
/// </summary>
public static class OutlookRecipients
{
    /// <summary>The addresses that follow the rule, normalized, without repeats and in order, and how many were left out.</summary>
    public static (IReadOnlyList<string> Valid, int Omitted) Normalize(IEnumerable<string?> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var valid = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var omitted = 0;
        foreach (var address in addresses)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            if (AgendaEmail.Normalize(address) is { } normalized)
            {
                if (seen.Add(normalized))
                {
                    valid.Add(normalized);
                }
            }
            else
            {
                omitted++;
            }
        }

        return (valid, omitted);
    }

    /// <summary>
    /// Whoever may hold the meeting (<paramref name="stored"/>) and is not among <paramref name="current"/>, filtered with the domains
    /// allowed now and the team address configured now (RF-3b). Sorted. Never shares an address with <paramref name="current"/>.
    /// </summary>
    public static IReadOnlyList<string> Removed(IReadOnlyList<string>? stored, IEnumerable<string> current, AgendaSettings settings)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(settings);
        if (stored is null || stored.Count == 0)
        {
            return [];
        }

        var keep = current.Select(AgendaEmail.Normalize).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return [.. Reachable(stored, settings).Where(address => !keep.Contains(address)).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The stored addresses a cancellation of the activity may go to (RF-3): the ones of an allowed domain or the team address. The
    /// others are not written to; they are forgotten when the cancellation is confirmed (accepted risk R-3).
    /// </summary>
    public static IReadOnlyList<string> Reachable(IReadOnlyList<string>? stored, AgendaSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (stored is null)
        {
            return [];
        }

        var domains = settings.AllowedAttendeeDomains.ToHashSet(StringComparer.Ordinal);
        var team = AgendaEmail.Normalize(settings.TeamAddress);
        return [.. stored
            .Select(AgendaEmail.Normalize)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Where(address => domains.Contains(AgendaEmail.DomainOf(address)) || string.Equals(address, team, StringComparison.Ordinal))];
    }
}

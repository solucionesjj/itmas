using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Outlook;

namespace Itmas3.UnitTests.Application.Agenda;

/// <summary>SPEC-0008 RF-3, RF-3b, CA-8 ("quienes se quitaron"): the pure recipient rules of the SMTP transport.</summary>
public sealed class RemovedRecipientsTests
{
    private static readonly AgendaSettings Settings = new()
    {
        TimeZone = TimeZoneInfo.Utc,
        AllowedAttendeeDomains = ["example.com"],
        TeamAddress = "Equipo@Lists.Example.org",
        OutlookEnabled = true,
    };

    [Fact]
    public void Whoever_may_hold_the_meeting_and_is_no_longer_invited_is_removed_without_telling_case_apart()
    {
        var removed = OutlookRecipients.Removed(["ana@example.com", "luis@example.com", "marta@example.com"], ["ANA@Example.com", "marta@example.com"], Settings);

        Assert.Equal(["luis@example.com"], removed);
    }

    [Fact]
    public void The_removed_never_share_an_address_with_the_current_recipients()
    {
        string[] stored = ["ana@example.com", "luis@example.com", "equipo@lists.example.org"];
        string[] current = ["Luis@example.com", "nuevo@example.com"];

        var removed = OutlookRecipients.Removed(stored, current, Settings);

        Assert.Empty(removed.Intersect(current.Select(address => address.ToLowerInvariant())));
        Assert.Equal(["ana@example.com", "equipo@lists.example.org"], removed);
    }

    [Fact]
    public void An_address_of_a_domain_that_is_no_longer_allowed_is_not_written_to()
    {
        var removed = OutlookRecipients.Removed(["ana@example.com", "otro@antes.example.net"], [], Settings);

        Assert.Equal(["ana@example.com"], removed);
        Assert.Equal(["ana@example.com"], OutlookRecipients.Reachable(["otro@antes.example.net", "ana@example.com"], Settings));
    }

    [Fact]
    public void The_team_address_configured_now_is_reachable_even_outside_the_allowed_domains()
    {
        Assert.Equal(["equipo@lists.example.org"], OutlookRecipients.Reachable(["equipo@lists.example.org"], Settings));
    }

    [Fact]
    public void Nothing_stored_means_nobody_to_remove() =>
        Assert.Empty(OutlookRecipients.Removed(null, ["ana@example.com"], Settings));

    [Fact]
    public void Normalizing_counts_what_breaks_the_rule_and_drops_repeats()
    {
        var (valid, omitted) = OutlookRecipients.Normalize(["Ana@example.com", "ana@example.com", "x;y@example.com", null, " "]);

        Assert.Equal(["ana@example.com"], valid);
        Assert.Equal(1, omitted);
    }
}

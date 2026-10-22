using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;
using static Itmas3.UnitTests.Domain.Agenda.AgendaTestData;

namespace Itmas3.UnitTests.Domain.Agenda;

/// <summary>SPEC-0006 RF-3, RN-1, RN-5 and CA-13: the descriptive data of an event.</summary>
public sealed class EventDetailsTests
{
    [Fact]
    public void Valid_data_is_normalized()
    {
        var details = Details(title: "  Parches de SO  ", participants: [" Ana.Perez@Example.com ", "luis@example.com"], scope: " ", location: " Remoto ");

        Assert.Equal("Parches de SO", details.Title);
        Assert.Equal(["ana.perez@example.com", "luis@example.com"], details.Participants);
        Assert.Null(details.Scope);
        Assert.Equal("Remoto", details.Location);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("titulo\u0007")]
    [InlineData("dos\nlíneas")]
    public void RN01_the_title_is_required_and_has_no_control_characters(string? title) =>
        Assert.Throws<DomainException>(() => Details(title: title));

    [Fact]
    public void RN01_the_title_admits_up_to_120_characters()
    {
        Assert.Equal(120, Details(title: new string('a', 120)).Title.Length);
        Assert.Throws<DomainException>(() => Details(title: new string('a', 121)));
    }

    [Fact]
    public void The_description_keeps_line_breaks_but_rejects_other_control_characters_and_long_text()
    {
        var ok = EventDetails.Create(TypeId, "t", "línea 1\nlínea 2\tfin", Responsible, null, AllowedDomains, true, EventImpact.None, null, null, 0);
        Assert.Equal("línea 1\nlínea 2\tfin", ok.Description);

        Assert.Throws<DomainException>(() => EventDetails.Create(TypeId, "t", "mal\u0007", Responsible, null, AllowedDomains, true, EventImpact.None, null, null, 0));
        Assert.Throws<DomainException>(() => EventDetails.Create(TypeId, "t", new string('a', 4001), Responsible, null, AllowedDomains, true, EventImpact.None, null, null, 0));
    }

    [Fact]
    public void The_scope_and_the_location_have_their_own_limits()
    {
        Assert.Equal(500, Details(scope: new string('a', 500)).Scope!.Length);
        Assert.Throws<DomainException>(() => Details(scope: new string('a', 501)));
        Assert.Equal(200, Details(location: new string('a', 200)).Location!.Length);
        Assert.Throws<DomainException>(() => Details(location: new string('a', 201)));
    }

    [Fact]
    public void The_type_and_the_responsible_are_required()
    {
        Assert.Throws<DomainException>(() =>
            EventDetails.Create(Guid.Empty, "t", null, Responsible, null, AllowedDomains, true, EventImpact.None, null, null, 0));
        Assert.Throws<DomainException>(() =>
            EventDetails.Create(TypeId, "t", null, Guid.Empty, null, AllowedDomains, true, EventImpact.None, null, null, 0));
    }

    [Fact]
    public void The_impact_must_be_a_known_value() =>
        Assert.Throws<DomainException>(() => Details(impact: (EventImpact)9));

    [Theory]
    [InlineData(-1)]
    [InlineData(40_321)]
    public void P25_the_reminder_goes_from_0_to_four_weeks(int minutes) =>
        Assert.Throws<DomainException>(() => Details(reminderMinutes: minutes));

    [Theory]
    [InlineData(0)]
    [InlineData(40_320)]
    public void P25_the_reminder_limits_are_inclusive(int minutes) =>
        Assert.Equal(minutes, Details(reminderMinutes: minutes).ReminderMinutes);

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("a@b")]
    [InlineData("a b@example.com")]
    [InlineData("a@@example.com")]
    [InlineData("")]
    public void CA13_participants_must_have_a_valid_address(string email) =>
        Assert.Throws<DomainException>(() => Details(participants: [email]));

    [Fact]
    public void CA13_participants_of_other_domains_are_rejected()
    {
        Assert.Throws<DomainException>(() => Details(participants: ["intruso@otro.com"]));
        Assert.Throws<DomainException>(() => Details(participants: ["intruso@sub.example.com"]));
        Assert.Throws<DomainException>(() => Details(participants: ["intruso@example.com.otro.com"]));
    }

    [Fact]
    public void CA13_repeated_participants_are_rejected_ignoring_case() =>
        Assert.Throws<DomainException>(() => Details(participants: ["ana@example.com", "ANA@example.com"]));

    [Fact]
    public void CA13_an_address_admits_up_to_254_characters()
    {
        var domain = "@example.com";
        Assert.Single(Details(participants: [new string('a', 254 - domain.Length) + domain]).Participants);
        Assert.Throws<DomainException>(() => Details(participants: [new string('a', 255 - domain.Length) + domain]));
    }

    [Fact]
    public void CA13_an_event_admits_30_participants_but_not_31()
    {
        IEnumerable<string> Many(int count) => Enumerable.Range(1, count).Select(i => $"persona{i}@example.com");

        Assert.Equal(30, Details(participants: Many(30)).Participants.Count);
        Assert.Throws<DomainException>(() => Details(participants: Many(31)));
    }

    [Fact]
    public void Without_allowed_domains_no_participant_can_be_added()
    {
        Assert.Empty(ParticipantList.Normalize(null, []));
        Assert.Throws<DomainException>(() => ParticipantList.Normalize(["ana@example.com"], []));
    }

    [Fact]
    public void The_allowed_domains_ignore_case_and_surrounding_spaces() =>
        Assert.Single(ParticipantList.Normalize(["ana@example.com"], [" EXAMPLE.com "]));
}

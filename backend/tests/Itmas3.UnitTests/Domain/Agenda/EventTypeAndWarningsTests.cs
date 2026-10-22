using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;
using static Itmas3.UnitTests.Domain.Agenda.AgendaTestData;

namespace Itmas3.UnitTests.Domain.Agenda;

/// <summary>SPEC-0006 RF-1, RF-2 and CA-1: the catalogue of types; RN-11 and CA-11: warnings.</summary>
public sealed class EventTypeAndWarningsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 20, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CA01_a_type_is_created_active_with_its_defaults()
    {
        var type = EventType.Create("  Hardening  ", "Revisión de líneas base", 90, 2880, isFreeze: false, Now);

        Assert.Equal("Hardening", type.Name);
        Assert.Equal(90, type.DefaultDurationMinutes);
        Assert.Equal(2880, type.DefaultReminderMinutes);
        Assert.True(type.IsActive);
        Assert.False(type.IsFreeze);
        Assert.Equal(1, type.Revision);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("tipo\u0007")]
    public void The_name_is_required_and_has_no_control_characters(string name) =>
        Assert.Throws<DomainException>(() => EventType.Create(name, null, 60, 0, false, Now));

    [Fact]
    public void The_name_admits_up_to_80_characters()
    {
        Assert.Equal(80, EventType.Create(new string('a', 80), null, 60, 0, false, Now).Name.Length);
        Assert.Throws<DomainException>(() => EventType.Create(new string('a', 81), null, 60, 0, false, Now));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(86_401, 0)]
    [InlineData(60, -1)]
    [InlineData(60, 40_321)]
    public void The_defaults_must_be_in_range(int duration, int reminder) =>
        Assert.Throws<DomainException>(() => EventType.Create("Tipo", null, duration, reminder, false, Now));

    [Fact]
    public void The_default_duration_can_be_up_to_60_days() =>
        Assert.Equal(86_400, EventType.Create("Tipo", null, 86_400, 0, false, Now).DefaultDurationMinutes);

    [Fact]
    public void RF02_updating_raises_the_revision_and_deactivating_is_idempotent()
    {
        var type = EventType.Create("Tipo", null, 60, 0, false, Now);

        type.Update("Tipo renombrado", "desc", 30, 10, isFreeze: true, isActive: true, Now.AddHours(1));
        Assert.Equal(2, type.Revision);
        Assert.True(type.IsFreeze);

        // Changing the data and deactivating at once is one change: the revision goes up once.
        type.Update("Tipo renombrado", "desc", 30, 10, isFreeze: true, isActive: false, Now.AddHours(1));
        Assert.Equal(3, type.Revision);
        Assert.False(type.IsActive);

        Assert.False(type.SetActive(false, Now.AddHours(3)));
        Assert.Equal(3, type.Revision);
        Assert.True(type.SetActive(true, Now.AddHours(4)));
        Assert.Equal(4, type.Revision);
    }

    private static OverlapCandidate Other(
        string title = "Otra",
        int startOffsetHours = 0,
        int hours = 2,
        EventStatus status = EventStatus.Scheduled,
        EventImpact impact = EventImpact.None,
        bool isFreeze = false) =>
        new(Guid.NewGuid(), title, Start.AddHours(startOffsetHours), Start.AddHours(startOffsetHours + hours), status, impact, isFreeze);

    private static IReadOnlyList<AgendaWarning> Evaluate(EventImpact impact, bool isFreeze, params OverlapCandidate[] others) =>
        AgendaWarnings.Evaluate(null, Start, Start.AddHours(2), isFreeze, impact, others);

    [Fact]
    public void RN11a_an_activity_overlapping_a_scheduled_freeze_gets_a_warning()
    {
        var freeze = Other("Cierre contable", isFreeze: true);

        var warnings = Evaluate(EventImpact.None, false, freeze);

        var warning = Assert.Single(warnings);
        Assert.Equal(AgendaWarningKind.FreezeOverlap, warning.Kind);
        Assert.Equal(freeze.Id, warning.EventId);
        Assert.Equal("Cierre contable", warning.EventTitle);
    }

    [Fact]
    public void RN11a_a_freeze_that_is_still_a_draft_does_not_warn_and_a_freeze_does_not_warn_about_freezes()
    {
        Assert.Empty(Evaluate(EventImpact.None, false, Other(status: EventStatus.Draft, isFreeze: true)));
        Assert.Empty(Evaluate(EventImpact.None, true, Other(isFreeze: true)));
    }

    [Fact]
    public void RN11b_two_activities_with_impact_that_overlap_warn_each_other()
    {
        var other = Other("Parches", impact: EventImpact.Outage, status: EventStatus.Draft);

        var warnings = Evaluate(EventImpact.Degradation, false, other);

        Assert.Equal(AgendaWarningKind.ImpactOverlap, Assert.Single(warnings).Kind);
    }

    [Fact]
    public void RN11b_without_impact_on_either_side_or_with_a_closed_activity_there_is_no_warning()
    {
        Assert.Empty(Evaluate(EventImpact.None, false, Other(impact: EventImpact.Outage)));
        Assert.Empty(Evaluate(EventImpact.Outage, false, Other(impact: EventImpact.None)));
        Assert.Empty(Evaluate(EventImpact.Outage, false, Other(impact: EventImpact.Outage, status: EventStatus.Completed)));
        Assert.Empty(Evaluate(EventImpact.Outage, false, Other(impact: EventImpact.Outage, status: EventStatus.Cancelled)));
    }

    [Fact]
    public void Touching_activities_and_the_activity_itself_do_not_overlap()
    {
        Assert.Empty(Evaluate(EventImpact.Outage, false, Other(startOffsetHours: 2, impact: EventImpact.Outage)));
        Assert.Empty(Evaluate(EventImpact.Outage, false, Other(startOffsetHours: -2, impact: EventImpact.Outage)));

        var itself = Other(impact: EventImpact.Outage);
        Assert.Empty(AgendaWarnings.Evaluate(itself.Id, Start, Start.AddHours(2), false, EventImpact.Outage, [itself]));
    }

    [Fact]
    public void An_activity_can_trigger_both_warnings_with_the_same_other_activity()
    {
        var freezeWithImpact = Other("Congelamiento", impact: EventImpact.Degradation, isFreeze: true);

        var warnings = Evaluate(EventImpact.Outage, false, freezeWithImpact);

        Assert.Equal([AgendaWarningKind.FreezeOverlap, AgendaWarningKind.ImpactOverlap], warnings.Select(w => w.Kind));
    }

    [Fact]
    public void Warnings_are_listed_in_chronological_order()
    {
        var later = Other("B", startOffsetHours: 1, impact: EventImpact.Outage);
        var earlier = Other("A", startOffsetHours: 0, impact: EventImpact.Outage);

        var warnings = Evaluate(EventImpact.Outage, false, later, earlier);

        Assert.Equal(["A", "B"], warnings.Select(w => w.EventTitle));
    }
}

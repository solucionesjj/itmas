using System.ComponentModel.DataAnnotations;
using Itmas3.Api.Contracts.Agenda;
using Itmas3.Application.Agenda.Queries;
using Itmas3.Domain.Agenda;

namespace Itmas3.UnitTests.Api.Agenda;

/// <summary>SPEC-0006 §12.1 and P-24: the local dates of the contract and the inclusive last day of all-day events.</summary>
public sealed class AgendaContractTests
{
    private static readonly Guid Id = Guid.Parse("0196b7a0-0000-7000-8000-000000000001");

    private static AgendaEventRequest Request(bool isAllDay, string? start, string? end) =>
        new(Id, "t", null, Id, null, null, null, null, null, null, null, isAllDay, start, end);

    private static List<ValidationResult> Validate(AgendaEventRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    [Theory]
    [InlineData("2026-10-24T09:00")]
    [InlineData("2026-10-24T09:00:30")]
    public void A_timed_event_accepts_local_date_times_to_the_minute_with_optional_seconds(string start) =>
        Assert.Empty(Validate(Request(false, start, "2026-10-24T11:00")));

    [Theory]
    [InlineData("2026-10-24")]
    [InlineData("2026-10-24T25:00")]
    [InlineData("2026-13-24T09:00")]
    [InlineData("2026-02-30T09:00")]
    [InlineData("mañana")]
    [InlineData("2026-10-24T09:00+05:00")]
    [InlineData("2026-10-24T09:00Z")]
    [InlineData(null)]
    public void A_timed_event_rejects_anything_that_is_not_a_local_date_time(string? start) =>
        Assert.NotEmpty(Validate(Request(false, start, "2026-10-24T11:00")));

    [Fact]
    public void A_timed_event_rejects_a_malformed_end() =>
        Assert.NotEmpty(Validate(Request(false, "2026-10-24T09:00", "2026-10-24")));

    [Fact]
    public void An_all_day_event_takes_dates_and_the_end_is_optional()
    {
        Assert.Empty(Validate(Request(true, "2026-10-26", "2026-10-28")));
        Assert.Empty(Validate(Request(true, "2026-10-26", null)));
        Assert.NotEmpty(Validate(Request(true, "2026-10-26T09:00", null)));
        Assert.NotEmpty(Validate(Request(true, "2026-02-30", null)));
        Assert.NotEmpty(Validate(Request(true, null, null)));
        Assert.NotEmpty(Validate(Request(true, "2026-10-26", "2026-10-30T10:00")));
    }

    [Fact]
    public void The_input_for_the_use_case_has_the_defaults_of_the_contract()
    {
        var input = Request(false, "2026-10-24T09:00", null).ToInput();

        Assert.True(input.NotifyTeam);
        Assert.Equal(EventImpact.None, input.Impact);
        Assert.Null(input.ReminderMinutes);
        Assert.False(input.Timing.IsAllDay);
        Assert.Equal(new DateTime(2026, 10, 24, 9, 0, 0), input.Timing.StartLocal);
        Assert.Null(input.Timing.EndLocal);
    }

    [Fact]
    public void An_explicit_notify_team_false_and_the_impact_are_kept()
    {
        var request = new AgendaEventRequest(Id, "t", null, Id, null, false, "outage", null, null, 30, null, true, "2026-10-24", "2026-10-25");

        var input = request.ToInput();

        Assert.False(input.NotifyTeam);
        Assert.Equal(EventImpact.Outage, input.Impact);
        Assert.Equal(30, input.ReminderMinutes);
        Assert.True(input.Timing.IsAllDay);
        Assert.Equal(new DateOnly(2026, 10, 24), input.Timing.FirstDay);
        Assert.Equal(new DateOnly(2026, 10, 25), input.Timing.LastDay);
    }

    [Fact]
    public void All_day_events_are_shown_with_the_inclusive_last_day_and_timed_ones_with_local_date_times()
    {
        var start = new DateTime(2026, 10, 26, 0, 0, 0);
        var end = new DateTime(2026, 10, 29, 0, 0, 0); // stored exclusive

        Assert.Equal("2026-10-26", AgendaMapping.Start(true, start));
        Assert.Equal("2026-10-28", AgendaMapping.End(true, end));
        Assert.Equal("2026-10-26T00:00:00", AgendaMapping.Start(false, start));
        Assert.Equal("2026-10-29T00:00:00", AgendaMapping.End(false, end));
        Assert.Equal("2026-10-24T09:30:00", AgendaMapping.Local(new DateTime(2026, 10, 24, 9, 30, 0)));
    }

    [Fact]
    public void An_all_day_event_of_one_day_shows_the_same_first_and_last_day()
    {
        var start = new DateTime(2026, 12, 31, 0, 0, 0);

        Assert.Equal(AgendaMapping.Start(true, start), AgendaMapping.End(true, start.AddDays(1)));
    }

    [Fact]
    public void The_series_rule_checks_its_anchor_and_its_end_date()
    {
        List<ValidationResult> Check(SeriesRuleRequest rule)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(rule, new ValidationContext(rule), results, validateAllProperties: true);
            return results;
        }

        Assert.Empty(Check(new SeriesRuleRequest("month", 1, new SeriesAnchorRequest("nth-weekday", null, 2, "tuesday"), null, 3)));
        Assert.Empty(Check(new SeriesRuleRequest("month", 1, new SeriesAnchorRequest("day-of-month", 31, null, null), "2027-12-31", null)));
        Assert.NotEmpty(Check(new SeriesRuleRequest("month", 1, new SeriesAnchorRequest("day-of-month", null, null, null), null, 3)));
        Assert.NotEmpty(Check(new SeriesRuleRequest("month", 1, new SeriesAnchorRequest("nth-weekday", null, 2, null), null, 3)));
        Assert.NotEmpty(Check(new SeriesRuleRequest("month", 1, null, "2027-02-30", null)));
        // The closed set of units is enforced by the MVC pipeline (the integration tests send "fortnight" and expect 400).
    }

    [Fact]
    public void The_series_rule_becomes_the_input_of_the_use_case()
    {
        var rule = new SeriesRuleRequest("month", 2, new SeriesAnchorRequest("nth-weekday", null, 2, "tuesday"), "2027-12-31", 10);

        var input = rule.ToInput();

        Assert.Equal(RecurrenceUnit.Month, input.Unit);
        Assert.Equal(2, input.Interval);
        Assert.Equal(RecurrenceAnchor.OnNthWeekday(2, DayOfWeek.Tuesday), input.Anchor);
        Assert.Equal(new DateOnly(2027, 12, 31), input.EndsOn);
        Assert.Equal(10, input.Count);
    }

    [Fact]
    public void The_status_filter_of_the_list_is_closed_to_the_known_values()
    {
        Assert.Matches(AgendaContract.StatusFilterPattern, "overdue");
        Assert.DoesNotMatch(AgendaContract.StatusFilterPattern, "archived");
        Assert.Equal(1000, AgendaLimits.MaxResults);
        Assert.Equal(366, AgendaLimits.MaxRangeDays);
    }
}

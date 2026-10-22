using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;
using static Itmas3.UnitTests.Domain.Agenda.AgendaTestData;

namespace Itmas3.UnitTests.Domain.Agenda;

/// <summary>SPEC-0006 RF-4, RN-6, RN-16, CA-3 and CA-29: the occurrences of a series.</summary>
public sealed class RecurrenceGeneratorTests
{
    private static IReadOnlyList<EventSchedule> Generate(RecurrencePattern pattern, EventSchedule template, TimeZoneInfo? zone = null) =>
        RecurrenceGenerator.Generate(pattern, template, zone ?? Bogota, Now);

    private static DateOnly[] Days(IEnumerable<EventSchedule> occurrences) => [.. occurrences.Select(o => o.FirstDay)];

    [Fact]
    public void CA03_the_second_tuesday_of_each_month_skips_the_one_before_the_start_date()
    {
        // The start date is Saturday 2026-10-17: the second Tuesday of October (the 13th) is already behind it.
        var template = Timed(2026, 10, 17, hour: 13); // later today in Bogotá
        var pattern = RecurrencePattern.Create(
            RecurrenceUnit.Month, 1, RecurrenceAnchor.OnNthWeekday(2, DayOfWeek.Tuesday), new DateOnly(2026, 10, 17), null, 4);

        var occurrences = Generate(pattern, template);

        Assert.Equal([new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 8), new DateOnly(2027, 1, 12), new DateOnly(2027, 2, 9)], Days(occurrences));
        Assert.All(occurrences, o => Assert.Equal(DayOfWeek.Tuesday, o.StartLocal.DayOfWeek));
    }

    [Fact]
    public void CA03_every_90_days_is_a_fixed_interval_from_the_start()
    {
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Day, 90, null, new DateOnly(2026, 10, 19), null, 4);

        var occurrences = Generate(pattern, Timed(2026, 10, 19));

        Assert.Equal([new DateOnly(2026, 10, 19), new DateOnly(2027, 1, 17), new DateOnly(2027, 4, 17), new DateOnly(2027, 7, 16)], Days(occurrences));
    }

    [Fact]
    public void CA03_every_two_weeks_keeps_the_weekday()
    {
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Week, 2, null, new DateOnly(2026, 10, 19), null, 3);

        Assert.Equal([new DateOnly(2026, 10, 19), new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 16)], Days(Generate(pattern, Timed(2026, 10, 19))));
    }

    [Fact]
    public void CA03_day_31_falls_on_the_last_day_of_shorter_months()
    {
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Month, 1, RecurrenceAnchor.OnDay(31), new DateOnly(2026, 10, 31), null, 5);

        var occurrences = Generate(pattern, Timed(2026, 10, 31));

        Assert.Equal(
            [new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28)],
            Days(occurrences));
    }

    [Fact]
    public void RN06_day_30_in_february_falls_on_the_last_day_even_in_a_leap_year()
    {
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Month, 1, RecurrenceAnchor.OnDay(30), new DateOnly(2027, 12, 30), null, 3);

        // 2028 is a leap year: February has 29 days.
        Assert.Equal([new DateOnly(2027, 12, 30), new DateOnly(2028, 1, 30), new DateOnly(2028, 2, 29)], Days(Generate(pattern, Timed(2027, 12, 30))));
    }

    [Fact]
    public void CA03_a_yearly_series_started_on_a_leap_day_falls_on_february_28_in_other_years()
    {
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Year, 1, null, new DateOnly(2028, 2, 29), null, 3);

        Assert.Equal([new DateOnly(2028, 2, 29), new DateOnly(2029, 2, 28), new DateOnly(2030, 2, 28)], Days(Generate(pattern, Timed(2028, 2, 29))));
    }

    [Fact]
    public void Quarterly_and_biannual_series_are_every_3_and_6_months()
    {
        var start = new DateOnly(2026, 10, 20);

        var quarterly = RecurrencePattern.Create(RecurrenceUnit.Month, 3, RecurrenceAnchor.OnDay(20), start, null, 3);
        var biannual = RecurrencePattern.Create(RecurrenceUnit.Month, 6, null, start, null, 3);

        Assert.Equal([new DateOnly(2026, 10, 20), new DateOnly(2027, 1, 20), new DateOnly(2027, 4, 20)], Days(Generate(quarterly, Timed(2026, 10, 20))));
        Assert.Equal([new DateOnly(2026, 10, 20), new DateOnly(2027, 4, 20), new DateOnly(2027, 10, 20)], Days(Generate(biannual, Timed(2026, 10, 20))));
    }

    [Fact]
    public void An_end_date_limits_the_series_and_is_inclusive()
    {
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Month, 1, RecurrenceAnchor.OnDay(20), new DateOnly(2026, 10, 20), new DateOnly(2027, 1, 20), null);

        var occurrences = Generate(pattern, Timed(2026, 10, 20));

        Assert.Equal(4, occurrences.Count);
        Assert.Equal(new DateOnly(2027, 1, 20), occurrences[^1].FirstDay);
    }

    [Fact]
    public void The_end_date_and_the_count_can_be_combined_and_the_first_limit_wins()
    {
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Day, 1, null, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 30), 3);

        Assert.Equal(3, Generate(pattern, Timed(2026, 10, 19)).Count);
    }

    [Fact]
    public void CA03_the_local_time_is_constant_when_the_utc_offset_changes()
    {
        // New York leaves standard time on 2027-03-14: 09:00 local is 14:00Z before and 13:00Z after.
        var template = EventSchedule.Timed(new DateTime(2027, 2, 10, 9, 0, 0), new DateTime(2027, 2, 10, 11, 30, 0), NewYork);
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Month, 1, RecurrenceAnchor.OnDay(10), new DateOnly(2027, 2, 10), null, 3);

        var occurrences = Generate(pattern, template, NewYork);

        Assert.All(occurrences, o =>
        {
            Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(o.StartLocal));
            Assert.Equal(TimeSpan.FromMinutes(150), o.EndLocal - o.StartLocal);
        });
        Assert.Equal([14, 14, 13], occurrences.Select(o => o.StartUtc.Hour));
    }

    [Fact]
    public void All_day_series_keep_their_length_in_days()
    {
        var template = EventSchedule.AllDay(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), Bogota);
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Month, 1, RecurrenceAnchor.OnDay(2), new DateOnly(2026, 11, 2), null, 3);

        var occurrences = Generate(pattern, template);

        Assert.All(occurrences, o =>
        {
            Assert.True(o.IsAllDay);
            Assert.Equal(2, o.LastDay.DayNumber - o.FirstDay.DayNumber + 1);
        });
        Assert.Equal([new DateOnly(2026, 11, 2), new DateOnly(2026, 12, 2), new DateOnly(2027, 1, 2)], Days(occurrences));
    }

    [Fact]
    public void CA03_more_than_60_occurrences_by_count_is_rejected()
    {
        Assert.Equal(60, Generate(RecurrencePattern.Create(RecurrenceUnit.Day, 1, null, new DateOnly(2026, 10, 19), null, 60), Timed(2026, 10, 19)).Count);
        Assert.Throws<DomainException>(() => RecurrencePattern.Create(RecurrenceUnit.Day, 1, null, new DateOnly(2026, 10, 19), null, 61));
    }

    [Fact]
    public void CA03_more_than_60_occurrences_by_end_date_is_rejected()
    {
        var sixtyOne = RecurrencePattern.Create(RecurrenceUnit.Day, 1, null, new DateOnly(2026, 10, 19), new DateOnly(2026, 12, 18), null);
        var sixty = RecurrencePattern.Create(RecurrenceUnit.Day, 1, null, new DateOnly(2026, 10, 19), new DateOnly(2026, 12, 17), null);

        Assert.Equal(60, Generate(sixty, Timed(2026, 10, 19)).Count);
        Assert.Throws<DomainException>(() => Generate(sixtyOne, Timed(2026, 10, 19)));
    }

    [Fact]
    public void An_end_date_far_in_the_future_with_a_dense_rule_is_rejected_without_running_forever()
    {
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Day, 1, null, new DateOnly(2026, 10, 19), new DateOnly(9999, 12, 31), null);

        Assert.Throws<DomainException>(() => Generate(pattern, Timed(2026, 10, 19)));
    }

    [Fact]
    public void CA29_a_series_that_starts_in_the_past_is_rejected()
    {
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Month, 1, RecurrenceAnchor.OnDay(10), new DateOnly(2026, 10, 10), null, 3);

        Assert.Throws<DomainException>(() => Generate(pattern, Timed(2026, 10, 10)));
    }

    [Fact]
    public void CA29_no_occurrence_is_earlier_than_the_start_date()
    {
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Month, 1, RecurrenceAnchor.OnDay(5), new DateOnly(2026, 10, 20), null, 3);

        var occurrences = Generate(pattern, Timed(2026, 10, 20));

        Assert.All(occurrences, o => Assert.True(o.FirstDay >= new DateOnly(2026, 10, 20)));
        Assert.Equal(new DateOnly(2026, 11, 5), occurrences[0].FirstDay);
    }

    [Fact]
    public void A_series_that_generates_nothing_is_rejected()
    {
        // The only candidate (the 5th of October) is before the start date and the end date leaves no other.
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Month, 1, RecurrenceAnchor.OnDay(5), new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 31), null);

        Assert.Throws<DomainException>(() => Generate(pattern, Timed(2026, 10, 20)));
    }

    [Fact]
    public void The_start_date_of_the_rule_must_be_the_first_day_of_the_template()
    {
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Day, 1, null, new DateOnly(2026, 10, 20), null, 3);

        Assert.Throws<DomainException>(() => Generate(pattern, Timed(2026, 10, 21)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void RN06_the_interval_goes_from_1_to_99(int interval) =>
        Assert.Throws<DomainException>(() => RecurrencePattern.Create(RecurrenceUnit.Day, interval, null, new DateOnly(2026, 10, 19), null, 3));

    [Fact]
    public void A_limit_is_required_and_the_end_cannot_precede_the_start()
    {
        Assert.Throws<DomainException>(() => RecurrencePattern.Create(RecurrenceUnit.Day, 1, null, new DateOnly(2026, 10, 19), null, null));
        Assert.Throws<DomainException>(() => RecurrencePattern.Create(RecurrenceUnit.Day, 1, null, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 18), null));
        Assert.Throws<DomainException>(() => RecurrencePattern.Create(RecurrenceUnit.Day, 1, null, new DateOnly(2026, 10, 19), null, 0));
    }

    [Fact]
    public void An_anchor_only_applies_to_months_and_years()
    {
        Assert.Throws<DomainException>(() =>
            RecurrencePattern.Create(RecurrenceUnit.Week, 1, RecurrenceAnchor.OnDay(3), new DateOnly(2026, 10, 19), null, 3));
    }

    [Fact]
    public void A_monthly_rule_without_anchor_repeats_on_the_day_of_the_start_date()
    {
        var pattern = RecurrencePattern.Create(RecurrenceUnit.Month, 1, null, new DateOnly(2026, 10, 22), null, 3);

        Assert.Equal(RecurrenceAnchor.OnDay(22), pattern.Anchor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void The_nth_weekday_goes_from_the_first_to_the_fourth(int nth) =>
        Assert.Throws<DomainException>(() => RecurrenceAnchor.OnNthWeekday(nth, DayOfWeek.Tuesday));

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void The_day_of_the_month_goes_from_1_to_31(int day) =>
        Assert.Throws<DomainException>(() => RecurrenceAnchor.OnDay(day));
}

using Itmas3.Domain.Agenda;
using Itmas3.Domain.Common;
using static Itmas3.UnitTests.Domain.Agenda.AgendaTestData;

namespace Itmas3.UnitTests.Domain.Agenda;

/// <summary>SPEC-0006 RN-2, RN-3, RN-16, RNF-4 and §10.5: the schedule of an event.</summary>
public sealed class EventScheduleTests
{
    [Fact]
    public void A_timed_event_keeps_the_local_time_and_computes_the_utc_instants()
    {
        var schedule = Timed(2026, 10, 20);

        Assert.Equal(new DateTime(2026, 10, 20, 9, 0, 0), schedule.StartLocal);
        Assert.Equal(new DateTime(2026, 10, 20, 11, 0, 0), schedule.EndLocal);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 14, 0, 0, TimeSpan.Zero), schedule.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 16, 0, 0, TimeSpan.Zero), schedule.EndUtc);
        Assert.Equal("America/Bogota", schedule.TimeZoneId);
        Assert.False(schedule.IsAllDay);
    }

    [Fact]
    public void Local_times_are_kept_to_the_minute()
    {
        var schedule = EventSchedule.Timed(new DateTime(2026, 10, 20, 9, 0, 45, 500), new DateTime(2026, 10, 20, 10, 30, 59), Bogota);

        Assert.Equal(new DateTime(2026, 10, 20, 9, 0, 0), schedule.StartLocal);
        Assert.Equal(new DateTime(2026, 10, 20, 10, 30, 0), schedule.EndLocal);
    }

    [Fact]
    public void The_kind_of_the_local_time_is_ignored_because_it_is_a_wall_clock_in_the_zone()
    {
        var utcKind = DateTime.SpecifyKind(new DateTime(2026, 10, 20, 9, 0, 0), DateTimeKind.Utc);

        var schedule = EventSchedule.Timed(utcKind, utcKind.AddHours(1), Bogota);

        Assert.Equal(new DateTimeOffset(2026, 10, 20, 14, 0, 0, TimeSpan.Zero), schedule.StartUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void RN02_the_end_must_be_after_the_start(int minutes)
    {
        var start = new DateTime(2026, 10, 20, 9, 0, 0);

        Assert.Throws<DomainException>(() => EventSchedule.Timed(start, start.AddMinutes(minutes), Bogota));
    }

    [Fact]
    public void RN02_an_activity_lasts_at_most_60_days()
    {
        var start = new DateTime(2026, 11, 1, 0, 0, 0);

        Assert.Equal(start.AddDays(60), EventSchedule.Timed(start, start.AddDays(60), Bogota).EndLocal);
        Assert.Throws<DomainException>(() => EventSchedule.Timed(start, start.AddDays(60).AddMinutes(1), Bogota));
    }

    [Fact]
    public void RN02_all_day_events_are_informed_with_inclusive_dates_and_stored_with_an_exclusive_end()
    {
        var schedule = EventSchedule.AllDay(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 22), Bogota);

        Assert.True(schedule.IsAllDay);
        Assert.Equal(new DateTime(2026, 10, 20, 0, 0, 0), schedule.StartLocal);
        Assert.Equal(new DateTime(2026, 10, 23, 0, 0, 0), schedule.EndLocal);
        Assert.Equal(new DateOnly(2026, 10, 20), schedule.FirstDay);
        Assert.Equal(new DateOnly(2026, 10, 22), schedule.LastDay);
    }

    [Fact]
    public void RN02_a_one_day_event_has_the_same_first_and_last_day()
    {
        var schedule = EventSchedule.AllDay(new DateOnly(2026, 12, 31), new DateOnly(2026, 12, 31), Bogota);

        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0), schedule.EndLocal);
        Assert.Equal(schedule.FirstDay, schedule.LastDay);
    }

    [Fact]
    public void RN02_an_all_day_event_lasts_at_most_60_days_counting_both_ends()
    {
        var first = new DateOnly(2026, 11, 1);

        Assert.Equal(first.AddDays(59), EventSchedule.AllDay(first, first.AddDays(59), Bogota).LastDay);
        Assert.Throws<DomainException>(() => EventSchedule.AllDay(first, first.AddDays(60), Bogota));
        Assert.Throws<DomainException>(() => EventSchedule.AllDay(first, first.AddDays(-1), Bogota));
    }

    [Fact]
    public void RN16_a_timed_event_is_in_the_past_when_its_start_instant_has_passed()
    {
        // Now is 12:00 in Bogotá.
        Assert.True(Timed(2026, 10, 17, hour: 11).IsInPast(Now, Bogota));
        Assert.False(Timed(2026, 10, 17, hour: 13).IsInPast(Now, Bogota));
    }

    [Fact]
    public void RN16_an_all_day_event_is_in_the_past_only_when_its_first_day_is_before_today()
    {
        Assert.False(EventSchedule.AllDay(new DateOnly(2026, 10, 17), new DateOnly(2026, 10, 17), Bogota).IsInPast(Now, Bogota));
        Assert.True(EventSchedule.AllDay(new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 18), Bogota).IsInPast(Now, Bogota));
    }

    [Fact]
    public void RN16_today_is_measured_in_the_organization_zone_not_in_utc()
    {
        // 2026-10-18 03:00 UTC is still 2026-10-17 22:00 in Bogotá.
        var lateEvening = new DateTimeOffset(2026, 10, 18, 3, 0, 0, TimeSpan.Zero);

        Assert.False(EventSchedule.AllDay(new DateOnly(2026, 10, 17), new DateOnly(2026, 10, 17), Bogota).IsInPast(lateEvening, Bogota));
    }

    [Fact]
    public void A_local_time_that_does_not_exist_moves_to_the_next_valid_minute()
    {
        // New York springs forward on 2027-03-14: 02:00 becomes 03:00, so 02:30 does not exist.
        var schedule = EventSchedule.Timed(new DateTime(2027, 3, 14, 2, 30, 0), new DateTime(2027, 3, 14, 5, 0, 0), NewYork);

        Assert.Equal(new DateTime(2027, 3, 14, 3, 0, 0), schedule.StartLocal);
        Assert.Equal(new DateTimeOffset(2027, 3, 14, 7, 0, 0, TimeSpan.Zero), schedule.StartUtc);
    }

    [Fact]
    public void An_ambiguous_local_time_takes_its_first_occurrence()
    {
        // New York falls back on 2026-11-01: 01:30 happens twice (EDT, then EST). The first one is UTC-4.
        var schedule = EventSchedule.Timed(new DateTime(2026, 11, 1, 1, 30, 0), new DateTime(2026, 11, 1, 4, 0, 0), NewYork);

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero), schedule.StartUtc);
        Assert.Equal(new DateTime(2026, 11, 1, 1, 30, 0), schedule.StartLocal);
    }

    [Fact]
    public void The_same_local_time_has_a_different_utc_instant_across_a_daylight_saving_change()
    {
        var winter = EventSchedule.Timed(new DateTime(2027, 2, 10, 9, 0, 0), new DateTime(2027, 2, 10, 10, 0, 0), NewYork);
        var summer = EventSchedule.Timed(new DateTime(2027, 4, 10, 9, 0, 0), new DateTime(2027, 4, 10, 10, 0, 0), NewYork);

        Assert.Equal(14, winter.StartUtc.Hour);
        Assert.Equal(13, summer.StartUtc.Hour);
    }

    [Fact]
    public void Overlaps_requires_sharing_time_and_touching_ends_do_not_count()
    {
        var schedule = Timed(2026, 10, 20); // 14:00Z - 16:00Z

        Assert.True(schedule.Overlaps(schedule.StartUtc.AddHours(1), schedule.EndUtc.AddHours(1)));
        Assert.False(schedule.Overlaps(schedule.EndUtc, schedule.EndUtc.AddHours(1)));
        Assert.False(schedule.Overlaps(schedule.StartUtc.AddHours(-1), schedule.StartUtc));
    }
}

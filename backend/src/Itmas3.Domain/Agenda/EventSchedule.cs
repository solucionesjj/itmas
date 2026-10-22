using Itmas3.Domain.Common;

namespace Itmas3.Domain.Agenda;

/// <summary>
/// When an event happens (SPEC-0006 RNF-4, §10.5): the local time of the organization time zone, that zone and the UTC
/// instants. For all-day events the stored end is <b>exclusive</b> (00:00 of the day after the last one), while callers
/// inform and read the last day inclusively (RN-2, P-24). Local times are kept to the minute.
/// </summary>
public sealed record EventSchedule(
    DateTime StartLocal,
    DateTime EndLocal,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string TimeZoneId,
    bool IsAllDay)
{
    /// <summary>Longest an activity can last (RN-2).</summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromDays(60);

    /// <summary>A timed event. The local times are interpreted in <paramref name="zone"/>; a time that does not exist (DST gap) moves to the next valid one.</summary>
    public static EventSchedule Timed(DateTime startLocal, DateTime endLocal, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var start = LocalTime.Resolve(startLocal, zone);
        var end = LocalTime.Resolve(endLocal, zone);
        return Build(start, end, zone, isAllDay: false);
    }

    /// <summary>An all-day event from its first to its last day, both inclusive (RN-2).</summary>
    public static EventSchedule AllDay(DateOnly firstDay, DateOnly lastDay, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (lastDay < firstDay)
        {
            throw new DomainException("El último día debe ser igual o posterior al primero.");
        }

        var start = LocalTime.Resolve(firstDay.ToDateTime(TimeOnly.MinValue), zone);
        var end = LocalTime.Resolve(lastDay.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        return Build(start, end, zone, isAllDay: true);
    }

    /// <summary>Last day (inclusive) of an all-day event; the end date for any other event.</summary>
    public DateOnly LastDay => DateOnly.FromDateTime(IsAllDay ? EndLocal.AddDays(-1) : EndLocal);

    public DateOnly FirstDay => DateOnly.FromDateTime(StartLocal);

    /// <summary>
    /// Whether the start is in the past, measured in the organization zone (RN-3, RN-16). An all-day event is in the past
    /// only when its first day is before today, so a full-day activity can still be planned for today.
    /// </summary>
    public bool IsInPast(DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return IsAllDay
            ? StartLocal.Date < TimeZoneInfo.ConvertTime(now, zone).Date
            : StartUtc < now;
    }

    /// <summary>Whether this schedule and the given UTC interval share any time (touching ends do not overlap).</summary>
    public bool Overlaps(DateTimeOffset startUtc, DateTimeOffset endUtc) => StartUtc < endUtc && startUtc < EndUtc;

    private static EventSchedule Build((DateTime Local, DateTimeOffset Utc) start, (DateTime Local, DateTimeOffset Utc) end, TimeZoneInfo zone, bool isAllDay)
    {
        if (end.Utc <= start.Utc || end.Local <= start.Local)
        {
            throw new DomainException("El fin debe ser posterior al inicio.");
        }

        return end.Local - start.Local > MaxDuration
            ? throw new DomainException("Una actividad no puede durar más de 60 días.")
            : new EventSchedule(start.Local, end.Local, start.Utc, end.Utc, zone.Id, isAllDay);
    }
}

/// <summary>Conversion of local wall-clock times of a zone into UTC instants (SPEC-0006 §10.5).</summary>
public static class LocalTime
{
    /// <summary>
    /// Resolves a local time (any <see cref="DateTimeKind"/>, interpreted as zone time) to the minute. A time that does not
    /// exist because of a DST gap moves to the first valid minute after the gap; an ambiguous time (DST end) takes its
    /// first occurrence.
    /// </summary>
    public static (DateTime Local, DateTimeOffset Utc) Resolve(DateTime local, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var wall = new DateTime(local.Ticks - (local.Ticks % TimeSpan.TicksPerMinute), DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(wall))
        {
            wall = wall.AddMinutes(1);
        }

        var offset = zone.IsAmbiguousTime(wall) ? zone.GetAmbiguousTimeOffsets(wall).Max() : zone.GetUtcOffset(wall);
        return (wall, new DateTimeOffset(wall, offset).ToUniversalTime());
    }
}

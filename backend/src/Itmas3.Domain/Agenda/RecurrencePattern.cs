using Itmas3.Domain.Common;

namespace Itmas3.Domain.Agenda;

/// <summary>Day picked by a monthly or yearly series (SPEC-0006 RF-4): a day number or the n-th weekday of the month.</summary>
public sealed record RecurrenceAnchor(RecurrenceAnchorKind Kind, int DayOfMonth, int Nth, DayOfWeek Weekday)
{
    /// <summary>The n-th weekday goes from the first to the fourth: a fifth one does not exist in every month.</summary>
    public const int MaxNth = 4;

    public static RecurrenceAnchor OnDay(int dayOfMonth) =>
        dayOfMonth is >= 1 and <= 31
            ? new RecurrenceAnchor(RecurrenceAnchorKind.DayOfMonth, dayOfMonth, 0, DayOfWeek.Sunday)
            : throw new DomainException("El día del mes debe estar entre 1 y 31.");

    public static RecurrenceAnchor OnNthWeekday(int nth, DayOfWeek weekday) =>
        nth is >= 1 and <= MaxNth && Enum.IsDefined(weekday)
            ? new RecurrenceAnchor(RecurrenceAnchorKind.NthWeekday, 0, nth, weekday)
            : throw new DomainException($"El n-ésimo día de la semana debe ir de 1 a {MaxNth}.");
}

/// <summary>
/// The repetition rule of a series (SPEC-0006 RF-4, RN-6): every N days, weeks, months or years, starting on a date and
/// limited by an end date, a number of occurrences (at most 60) or both. It is stored only to describe the series (RN-12):
/// the occurrences exist as rows of their own.
/// </summary>
public sealed record RecurrencePattern(
    RecurrenceUnit Unit,
    int Interval,
    RecurrenceAnchor? Anchor,
    DateOnly StartsOn,
    DateOnly? EndsOn,
    int? Count)
{
    public const int MaxInterval = 99;
    public const int MaxOccurrences = 60;

    /// <summary>Validates the rule. Months and years without an anchor repeat on the day of <paramref name="startsOn"/>.</summary>
    public static RecurrencePattern Create(RecurrenceUnit unit, int interval, RecurrenceAnchor? anchor, DateOnly startsOn, DateOnly? endsOn, int? count)
    {
        if (!Enum.IsDefined(unit))
        {
            throw new DomainException("La unidad de repetición no es válida.");
        }

        if (interval is < 1 or > MaxInterval)
        {
            throw new DomainException($"La repetición debe ser cada 1 a {MaxInterval}.");
        }

        var monthly = unit is RecurrenceUnit.Month or RecurrenceUnit.Year;
        if (anchor is not null && !monthly)
        {
            throw new DomainException("El ancla solo aplica a las repeticiones mensuales y anuales.");
        }

        if (endsOn is null && count is null)
        {
            throw new DomainException("La serie necesita una fecha final o un número de ocurrencias.");
        }

        if (count is < 1 or > MaxOccurrences)
        {
            throw new DomainException($"Una serie admite de 1 a {MaxOccurrences} ocurrencias.");
        }

        return endsOn is { } end && end < startsOn
            ? throw new DomainException("La fecha final no puede ser anterior a la de inicio.")
            : new RecurrencePattern(unit, interval, monthly ? anchor ?? RecurrenceAnchor.OnDay(startsOn.Day) : null, startsOn, endsOn, count);
    }
}

/// <summary>
/// Generates the occurrences of a series (SPEC-0006 RF-4, RN-6, RN-16, CA-3). It works on local dates and resolves each one
/// with the organization zone, so "09:00" stays "09:00" when the UTC offset changes.
/// </summary>
public static class RecurrenceGenerator
{
    /// <summary>
    /// Occurrences of <paramref name="pattern"/> from <paramref name="template"/> (the schedule of the first one: its time of
    /// day and duration are kept). None is earlier than the start date (a monthly "second Tuesday" that falls before it is
    /// skipped); a day that does not exist in a month falls on its last day. Throws when the start is in the past, when
    /// nothing is generated or when more than 60 would be.
    /// </summary>
    public static IReadOnlyList<EventSchedule> Generate(RecurrencePattern pattern, EventSchedule template, TimeZoneInfo zone, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(zone);
        if (template.FirstDay != pattern.StartsOn)
        {
            throw new DomainException("La fecha de inicio de la serie no coincide con la de la primera actividad.");
        }

        if (template.IsInPast(now, zone))
        {
            throw new DomainException("El inicio no puede estar en el pasado.");
        }

        var occurrences = new List<EventSchedule>();
        var limit = pattern.Count ?? RecurrencePattern.MaxOccurrences;
        foreach (var date in Dates(pattern))
        {
            if (date < pattern.StartsOn)
            {
                continue;
            }

            if (pattern.EndsOn is { } end && date > end)
            {
                break;
            }

            if (occurrences.Count == limit)
            {
                if (pattern.Count is null)
                {
                    throw new DomainException($"Una serie admite como máximo {RecurrencePattern.MaxOccurrences} ocurrencias.");
                }

                break;
            }

            occurrences.Add(Occurrence(template, date, zone));
        }

        return occurrences.Count > 0
            ? occurrences
            : throw new DomainException("La serie no genera ninguna actividad.");
    }

    private static EventSchedule Occurrence(EventSchedule template, DateOnly date, TimeZoneInfo zone)
    {
        if (template.IsAllDay)
        {
            var lastDay = date.AddDays(template.LastDay.DayNumber - template.FirstDay.DayNumber);
            return EventSchedule.AllDay(date, lastDay, zone);
        }

        // The local duration is kept, so an occurrence that crosses a DST change keeps its wall-clock end.
        var start = date.ToDateTime(TimeOnly.FromDateTime(template.StartLocal));
        return EventSchedule.Timed(start, start + (template.EndLocal - template.StartLocal), zone);
    }

    private static IEnumerable<DateOnly> Dates(RecurrencePattern pattern)
    {
        var start = pattern.StartsOn;
        for (var step = 0; step < 100_000; step++)
        {
            DateOnly? date = pattern.Unit switch
            {
                RecurrenceUnit.Day => Add(start, days: (long)step * pattern.Interval),
                RecurrenceUnit.Week => Add(start, days: 7L * step * pattern.Interval),
                RecurrenceUnit.Month => Anchored((12L * start.Year) + start.Month - 1 + ((long)step * pattern.Interval), pattern),
                _ => Anchored((12L * (start.Year + ((long)step * pattern.Interval))) + start.Month - 1, pattern),
            };

            if (date is null)
            {
                yield break;
            }

            yield return date.Value;
        }
    }

    private static DateOnly? Add(DateOnly start, long days) =>
        days > DateOnly.MaxValue.DayNumber - start.DayNumber ? null : start.AddDays((int)days);

    // monthIndex = year * 12 + (month - 1); null when the year leaves the supported range.
    private static DateOnly? Anchored(long monthIndex, RecurrencePattern pattern)
    {
        if (monthIndex / 12 > 9998)
        {
            return null;
        }

        var year = (int)(monthIndex / 12);
        var month = (int)(monthIndex % 12) + 1;

        var anchor = pattern.Anchor ?? RecurrenceAnchor.OnDay(pattern.StartsOn.Day);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        if (anchor.Kind == RecurrenceAnchorKind.DayOfMonth)
        {
            return new DateOnly(year, month, Math.Min(anchor.DayOfMonth, daysInMonth));
        }

        var first = new DateOnly(year, month, 1);
        var offset = ((int)anchor.Weekday - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(offset + (7 * (anchor.Nth - 1)));
    }
}

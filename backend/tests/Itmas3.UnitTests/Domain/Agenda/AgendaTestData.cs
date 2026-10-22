using Itmas3.Domain.Agenda;

namespace Itmas3.UnitTests.Domain.Agenda;

/// <summary>Shared fixtures of the agenda tests: a fixed "now", the zones and valid building blocks.</summary>
internal static class AgendaTestData
{
    /// <summary>Saturday 2026-10-17 12:00 in Bogotá (17:00 UTC).</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 17, 17, 0, 0, TimeSpan.Zero);

    public static readonly TimeZoneInfo Bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    /// <summary>A zone with daylight saving time, to prove the local time is kept when the UTC offset changes.</summary>
    public static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public static readonly Guid TypeId = Guid.Parse("0196b7a0-0000-7000-8000-000000000001");
    public static readonly Guid Responsible = Guid.Parse("0196b7a0-0000-7000-8000-000000000002");
    public static readonly Guid Actor = Guid.Parse("0196b7a0-0000-7000-8000-000000000003");

    public static readonly string[] AllowedDomains = ["example.com"];

    public static EventDetails Details(
        string? title = "Parches de SO",
        IEnumerable<string>? participants = null,
        bool notifyTeam = true,
        EventImpact impact = EventImpact.None,
        string? scope = "Servidores Windows",
        string? location = "Remoto",
        int reminderMinutes = 1440,
        Guid? responsible = null) =>
        EventDetails.Create(
            TypeId, title, "Aplicar los parches del mes", responsible ?? Responsible, participants, AllowedDomains, notifyTeam, impact, scope, location, reminderMinutes);

    /// <summary>A two-hour activity at 09:00 Bogotá on the given day.</summary>
    public static EventSchedule Timed(int year, int month, int day, int hour = 9, int hours = 2) =>
        EventSchedule.Timed(new DateTime(year, month, day, hour, 0, 0), new DateTime(year, month, day, hour, 0, 0).AddHours(hours), Bogota);

    public static EventSchedule NextWeek() => Timed(2026, 10, 24);

    public static AgendaEvent Draft(EventSchedule? schedule = null, EventDetails? details = null) =>
        AgendaEvent.Create(details ?? Details(), schedule ?? NextWeek(), Now, Actor);

    public static AgendaEvent Scheduled(EventSchedule? schedule = null, EventDetails? details = null)
    {
        var agendaEvent = Draft(schedule, details);
        agendaEvent.Schedule(Now, Bogota, Actor);
        return agendaEvent;
    }
}

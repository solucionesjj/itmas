using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Itmas3.Application.Agenda;
using Itmas3.Domain.Agenda;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>
/// Helpers of the SPEC-0006 acceptance tests: a user with every agenda permission, bodies with sensible defaults, dates relative
/// to the clock of the machine (always in the future of the organization zone) and direct database seeds for what the API
/// cannot create, such as events that already started (RN-16).
/// </summary>
public abstract class AgendaTestBase(PostgreSqlFixture database) : ApiTestBase(database)
{
    protected static readonly string[] AllPermissions =
    [
        AgendaPermissions.Read, AgendaPermissions.Create, AgendaPermissions.Update,
        AgendaPermissions.Publish, AgendaPermissions.Complete, AgendaPermissions.TypesManage,
    ];

    /// <summary>"Mantenimiento y parches de sistema operativo": the first type seeded by the migration (60 minutes, reminder 1 day).</summary>
    protected static readonly Guid PatchingTypeId = Guid.Parse("019ba5c0-0000-7000-8000-000000000001");

    /// <summary>"Congelamiento de cambios": the seeded freeze type (1 day).</summary>
    protected static readonly Guid FreezeTypeId = Guid.Parse("019ba5c0-0000-7000-8000-00000000000b");

    protected static readonly TimeZoneInfo Bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    protected static readonly Uri Events = Url("/api/v1/agenda-events");
    protected static readonly Uri Series = Url("/api/v1/agenda-series");
    protected static readonly Uri Types = Url("/api/v1/agenda-event-types");
    protected static readonly Uri Tags = Url("/api/v1/agenda-tags");

    protected static Uri Url(string path) => new(path, UriKind.Relative);

    protected static Uri EventUrl(Guid id, string? suffix = null) => Url($"/api/v1/agenda-events/{id}{suffix}");

    protected Task<HttpClient> AdminAsync() => Factory.CreateAuthenticatedClientAsync(false, AllPermissions);

    protected static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    /// <summary>The current time in the organization zone.</summary>
    protected static DateTime LocalNow() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Bogota);

    /// <summary>A local date-time <paramref name="daysAhead"/> days from today at <paramref name="hour"/>, to the minute (always in the future).</summary>
    protected static string Future(int daysAhead, int hour = 9, int minute = 0) =>
        LocalNow().Date.AddDays(daysAhead).AddHours(hour).AddMinutes(minute).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    protected static string FutureDay(int daysAhead) =>
        LocalNow().Date.AddDays(daysAhead).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    protected static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>The body of an event with defaults: a two-hour activity in 10 days at 09:00, type of patches.</summary>
    protected static Dictionary<string, object?> EventBody(
        Guid responsible,
        string? title = null,
        Guid? typeId = null,
        string? start = null,
        string? end = null,
        bool isAllDay = false,
        string[]? participants = null,
        string[]? tags = null,
        string impact = "none",
        int? reminderMinutes = null,
        bool? notifyTeam = null) =>
        new()
        {
            ["typeId"] = typeId ?? PatchingTypeId,
            ["title"] = title ?? "Parches de SO",
            ["description"] = "Aplicar los parches del mes",
            ["responsibleUserId"] = responsible,
            ["participants"] = participants,
            ["notifyTeam"] = notifyTeam,
            ["impact"] = impact,
            ["scope"] = "Servidores Windows",
            ["location"] = "Remoto",
            ["reminderMinutes"] = reminderMinutes,
            ["tags"] = tags,
            ["isAllDay"] = isAllDay,
            ["start"] = start ?? (isAllDay ? FutureDay(10) : Future(10)),
            ["end"] = end ?? DefaultEnd(isAllDay, start),
        };

    /// <summary>Two hours after the given start (or 09:00-11:00 in 10 days); none for all-day events, which then last one day.</summary>
    private static string? DefaultEnd(bool isAllDay, string? start) =>
        isAllDay
            ? null
            : start is null
                ? Future(10, 11)
                : DateTime.ParseExact(start, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture).AddHours(2).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    protected async Task<TestUser> UserAsync() => await Factory.CreateUserAsync();

    /// <summary>Creates an event through the API and returns its JSON (asserting 201).</summary>
    protected async Task<JsonElement> CreateEventAsync(HttpClient client, Dictionary<string, object?> body)
    {
        var response = await client.PostAsJsonAsync(Events, body, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    protected async Task<ScheduleOutcome> ScheduleAsync(HttpClient client, params Guid[] ids)
    {
        var response = await client.PostAsJsonAsync(Url("/api/v1/agenda-events/schedule"), new { ids }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new ScheduleOutcome(await ReadJsonAsync(response));
    }

    protected async Task<JsonElement> GetEventAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync(EventUrl(id), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    /// <summary>
    /// The warnings of a response that are about the given events. Freezes and overlaps with impact are global and the database is
    /// shared by the whole run, so another test may add warnings of its own on the same days.
    /// </summary>
    protected static List<JsonElement> WarningsAbout(JsonElement json, params Guid[] eventIds) =>
        [.. json.GetProperty("warnings").EnumerateArray().Where(warning => eventIds.Contains(warning.GetProperty("eventId").GetGuid()))];

    protected static async Task<string> ProblemDetailAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return json.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String ? detail.GetString()! : string.Empty;
    }

    protected static HttpRequestMessage Put(Uri uri, object body, int? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, uri) { Content = JsonContent.Create(body) };
        if (ifMatch is { } revision)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{revision}\"");
        }

        return request;
    }

    /// <summary>Seeds an event directly in the database, "as of" a moment in the past, to get states the API cannot reach today.</summary>
    protected async Task<Guid> SeedEventAsync(
        Guid responsible,
        DateTime startLocal,
        string title = "Evento sembrado",
        int hours = 2,
        Guid? typeId = null,
        EventStatus status = EventStatus.Scheduled,
        EventImpact impact = EventImpact.None,
        string[]? tags = null,
        bool allDay = false)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var recordedAt = new DateTimeOffset(startLocal, TimeSpan.FromHours(-5)).AddDays(-3).ToUniversalTime();
        var details = EventDetails.Create(typeId ?? PatchingTypeId, title, null, responsible, null, ["example.com"], true, impact, null, null, 1440);
        var schedule = allDay
            ? EventSchedule.AllDay(DateOnly.FromDateTime(startLocal), DateOnly.FromDateTime(startLocal), Bogota)
            : EventSchedule.Timed(startLocal, startLocal.AddHours(hours), Bogota);
        var agendaEvent = AgendaEvent.Create(details, schedule, recordedAt, responsible);

        if (status != EventStatus.Draft)
        {
            agendaEvent.Schedule(recordedAt, Bogota, responsible);
        }

        if (status == EventStatus.Completed)
        {
            agendaEvent.Complete(CompletionResult.Succeeded, null, "Hecho", null, schedule.EndUtc.AddHours(1), responsible);
        }
        else if (status == EventStatus.Cancelled)
        {
            agendaEvent.Cancel("Se descartó", recordedAt.AddHours(1), responsible);
        }

        if (tags is { Length: > 0 })
        {
            var tagList = new List<AgendaTag>();
            foreach (var name in tags)
            {
                var key = name.ToLowerInvariant();
                var tag = await db.AgendaTags.SingleOrDefaultAsync(existing => existing.Name.ToLower() == key, Ct)
                    ?? db.AgendaTags.Add(AgendaTag.Create(name, recordedAt, responsible)).Entity;
                tagList.Add(tag);
            }

            agendaEvent.ReplaceTags(tagList);
        }

        db.AgendaEvents.Add(agendaEvent);
        await db.SaveChangesAsync(Ct);
        return agendaEvent.Id;
    }

    protected async Task<List<(string Action, string ResourceType, string? ResourceId, string? Before, string? After)>> AuditAsync(string actionPrefix)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.AuditEvents.Where(audit => audit.Action.StartsWith(actionPrefix)).OrderBy(audit => audit.OccurredAt).ToListAsync(Ct);
        return [.. rows.Select(row => (row.Action, row.ResourceType, row.ResourceId, row.Before, row.After))];
    }

    /// <summary>The JSON of the result of scheduling, with a helper to read the entry of one event.</summary>
    protected sealed class ScheduleOutcome(JsonElement json)
    {
        public JsonElement Json { get; } = json;

        public JsonElement Of(Guid id) => Json.GetProperty("results").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id);
    }
}

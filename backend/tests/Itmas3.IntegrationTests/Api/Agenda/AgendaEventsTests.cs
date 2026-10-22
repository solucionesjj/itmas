using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Itmas3.Domain.Agenda;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>SPEC-0006 CA-2, CA-4, CA-6 to CA-9, CA-11 to CA-13 and CA-29: events and their lifecycle through the real API and PostgreSQL.</summary>
public sealed class AgendaEventsTests(PostgreSqlFixture database) : AgendaTestBase(database)
{
    [Fact]
    public async Task CA02_an_event_is_created_in_draft_with_the_given_data_and_the_reminder_of_its_type()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();

        var response = await admin.PostAsJsonAsync(
            Events, EventBody(responsible.Id, participants: ["ana@example.com"], impact: "degradation", tags: ["windows"]), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
        var json = await ReadJsonAsync(response);
        Assert.Equal($"/api/v1/agenda-events/{json.GetProperty("id").GetGuid()}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("draft", json.GetProperty("status").GetString());
        Assert.Equal(1, json.GetProperty("revision").GetInt32());
        Assert.Equal("Parches de SO", json.GetProperty("title").GetString());
        Assert.Equal(1440, json.GetProperty("reminderMinutes").GetInt32());
        Assert.Equal("degradation", json.GetProperty("impact").GetString());
        Assert.True(json.GetProperty("notifyTeam").GetBoolean());
        Assert.Equal(["ana@example.com"], json.GetProperty("participants").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(["windows"], json.GetProperty("tags").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal("America/Bogota", json.GetProperty("timeZone").GetString());
        Assert.Equal(responsible.Id, json.GetProperty("responsible").GetProperty("id").GetGuid());
        Assert.Equal("not-published", json.GetProperty("outlook").GetProperty("status").GetString());
        Assert.Equal(PatchingTypeId, json.GetProperty("type").GetProperty("id").GetGuid());
        Assert.False(json.GetProperty("isOverdue").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("completion").ValueKind);
    }

    [Fact]
    public async Task CA02_the_local_time_the_zone_and_the_utc_instants_are_returned()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var start = Future(10, 9);

        var json = await CreateEventAsync(admin, EventBody(responsible.Id, start: start, end: Future(10, 11)));

        Assert.Equal($"{start}:00", json.GetProperty("start").GetString());
        var startUtc = json.GetProperty("startUtc").GetDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, startUtc.Offset);
        Assert.Equal(14, startUtc.Hour); // 09:00 in Bogotá (UTC-5)
        Assert.Equal(16, json.GetProperty("endUtc").GetDateTimeOffset().Hour);
    }

    [Fact]
    public async Task CA02_all_day_events_use_dates_with_an_inclusive_last_day()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();

        var json = await CreateEventAsync(admin, EventBody(responsible.Id, isAllDay: true, start: FutureDay(10), end: FutureDay(12)));

        Assert.True(json.GetProperty("isAllDay").GetBoolean());
        Assert.Equal(FutureDay(10), json.GetProperty("start").GetString());
        Assert.Equal(FutureDay(12), json.GetProperty("end").GetString());
        var spanDays = (json.GetProperty("endUtc").GetDateTimeOffset() - json.GetProperty("startUtc").GetDateTimeOffset()).TotalDays;
        Assert.Equal(3, spanDays); // three whole days: the stored end is exclusive
    }

    [Fact]
    public async Task CA02_a_missing_end_uses_the_default_duration_of_the_type()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var body = EventBody(responsible.Id);
        body["end"] = null;

        var json = await CreateEventAsync(admin, body);

        Assert.Equal(60, (json.GetProperty("endUtc").GetDateTimeOffset() - json.GetProperty("startUtc").GetDateTimeOffset()).TotalMinutes);
    }

    [Theory]
    [InlineData("""{"typeId":"019ba5c0-0000-7000-8000-000000000001","title":"x","responsibleUserId":"RESP","isAllDay":false,"start":"2099-01-01T09:00","unknown":1}""")]
    [InlineData("""{"typeId":"019ba5c0-0000-7000-8000-000000000001","title":"x","responsibleUserId":"RESP","isAllDay":false,"start":"mañana"}""")]
    [InlineData("""{"typeId":"019ba5c0-0000-7000-8000-000000000001","title":"x","responsibleUserId":"RESP","isAllDay":false,"start":"2099-01-01"}""")]
    [InlineData("""{"typeId":"019ba5c0-0000-7000-8000-000000000001","title":"x","responsibleUserId":"RESP","isAllDay":true,"start":"2099-01-01T09:00"}""")]
    [InlineData("""{"typeId":"019ba5c0-0000-7000-8000-000000000001","title":"x","responsibleUserId":"RESP","impact":"catastrophic","isAllDay":false,"start":"2099-01-01T09:00"}""")]
    [InlineData("""{"typeId":"019ba5c0-0000-7000-8000-000000000001","title":"x","responsibleUserId":"RESP","isAllDay":false,"start":"2099-02-31T09:00"}""")]
    public async Task CA02_malformed_bodies_and_unknown_properties_are_rejected_with_400(string body)
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();

        var response = await admin.PostAsync(Events, Json(body.Replace("RESP", responsible.Id.ToString(), StringComparison.Ordinal)), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CA02_the_business_rules_of_the_data_answer_422()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var withoutResponsible = EventBody(responsible.Id, "Sin responsable");
        withoutResponsible["responsibleUserId"] = Guid.Empty;
        Dictionary<string, object?>[] invalid =
        [
            EventBody(responsible.Id, title: ""),
            EventBody(responsible.Id, title: new string('a', 121)),
            EventBody(responsible.Id, title: "con\u0007control"),
            EventBody(responsible.Id, typeId: Guid.NewGuid()),
            withoutResponsible,
            EventBody(responsible.Id, reminderMinutes: 99_999),
            EventBody(responsible.Id, start: Future(10, 11), end: Future(10, 9)),
            EventBody(responsible.Id, start: Future(10, 9), end: Future(80, 9)),
        ];

        foreach (var body in invalid)
        {
            var response = await admin.PostAsJsonAsync(Events, body, Ct);
            Assert.True(response.StatusCode == HttpStatusCode.UnprocessableEntity, $"{body["title"]} → {response.StatusCode}");
        }
    }

    [Fact]
    public async Task CA12_a_responsible_that_does_not_exist_or_is_inactive_cannot_be_assigned()
    {
        var admin = await AdminAsync();
        var inactive = await UserAsync();
        await SetActiveAsync(inactive.Id, false);

        var unknown = await admin.PostAsJsonAsync(Events, EventBody(Guid.NewGuid()), Ct);
        var disabled = await admin.PostAsJsonAsync(Events, EventBody(inactive.Id), Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, disabled.StatusCode);
    }

    [Fact]
    public async Task CA12_a_responsible_deactivated_later_is_shown_as_inactive_and_does_not_block_editing()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var created = await CreateEventAsync(admin, EventBody(responsible.Id));
        var id = created.GetProperty("id").GetGuid();

        await SetActiveAsync(responsible.Id, false);

        var detail = await GetEventAsync(admin, id);
        Assert.False(detail.GetProperty("responsible").GetProperty("isActive").GetBoolean());
        var edit = await admin.SendAsync(Put(EventUrl(id), EventBody(responsible.Id, "Sigue editable"), 1), Ct);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var other = await UserAsync();
        await SetActiveAsync(other.Id, false);
        var reassign = await admin.SendAsync(Put(EventUrl(id), EventBody(other.Id), 2), Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reassign.StatusCode);
    }

    [Fact]
    public async Task CA13_participants_are_limited_to_the_allowed_domain_unique_valid_and_at_most_30()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        string[][] invalid =
        [
            ["intruso@otro.com"],
            ["ana@example.com", "ANA@example.com"],
            ["sin-arroba"],
            [.. Enumerable.Range(1, 31).Select(i => $"persona{i}@example.com")],
        ];

        foreach (var participants in invalid)
        {
            var response = await admin.PostAsJsonAsync(Events, EventBody(responsible.Id, participants: participants), Ct);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        var ok = await CreateEventAsync(admin, EventBody(responsible.Id, participants: [.. Enumerable.Range(1, 30).Select(i => $"persona{i}@example.com")]));
        Assert.Equal(30, ok.GetProperty("participants").GetArrayLength());
    }

    [Fact]
    public async Task CA29_an_event_that_starts_in_the_past_is_rejected_but_one_later_today_is_accepted()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var past = LocalNow().AddMinutes(-30).ToString("yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        // A few minutes ahead: always in the future when the request arrives, and still today except in the last minutes of the day,
        // so the accepted case is never skipped whatever the time of the run.
        var later = LocalNow().AddMinutes(5);

        var rejected = await admin.PostAsJsonAsync(Events, EventBody(responsible.Id, start: past, end: Future(0, 23, 59)), Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);

        var accepted = await admin.PostAsJsonAsync(
            Events,
            EventBody(responsible.Id, start: later.ToString("yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture), end: later.AddMinutes(30).ToString("yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture)),
            Ct);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);

        var allDayToday = await admin.PostAsJsonAsync(Events, EventBody(responsible.Id, isAllDay: true, start: FutureDay(0), end: FutureDay(0)), Ct);
        Assert.Equal(HttpStatusCode.Created, allDayToday.StatusCode);
        var allDayYesterday = await admin.PostAsJsonAsync(Events, EventBody(responsible.Id, isAllDay: true, start: FutureDay(-1), end: FutureDay(0)), Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, allDayYesterday.StatusCode);
    }

    [Fact]
    public async Task CA04_a_stale_if_match_answers_412_and_a_missing_one_428_and_nothing_changes()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var id = (await CreateEventAsync(admin, EventBody(responsible.Id))).GetProperty("id").GetGuid();

        var ok = await admin.SendAsync(Put(EventUrl(id), EventBody(responsible.Id, "Primera edición"), 1), Ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("\"2\"", ok.Headers.ETag?.Tag);

        var stale = await admin.SendAsync(Put(EventUrl(id), EventBody(responsible.Id, "Segunda edición"), 1), Ct);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        var missing = await admin.SendAsync(Put(EventUrl(id), EventBody(responsible.Id, "Sin versión"), null), Ct);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);

        Assert.Equal("Primera edición", (await GetEventAsync(admin, id)).GetProperty("title").GetString());
        var notFound = await admin.SendAsync(Put(EventUrl(Guid.NewGuid()), EventBody(responsible.Id), 1), Ct);
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task CA04_a_completed_event_admits_no_update_and_a_cancelled_one_neither()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var completed = await SeedEventAsync(responsible.Id, LocalNow().AddDays(-2), status: EventStatus.Completed);
        var cancelled = await SeedEventAsync(responsible.Id, LocalNow().AddDays(5), status: EventStatus.Cancelled);

        var completedUpdate = await admin.SendAsync(Put(EventUrl(completed), EventBody(responsible.Id), 3), Ct);
        var cancelledUpdate = await admin.SendAsync(Put(EventUrl(cancelled), EventBody(responsible.Id), 3), Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, completedUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cancelledUpdate.StatusCode);
    }

    [Fact]
    public async Task CA06_a_draft_is_deleted_and_the_rest_cannot_be_deleted()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var draft = (await CreateEventAsync(admin, EventBody(responsible.Id, tags: ["borrar"]))).GetProperty("id").GetGuid();
        var scheduled = (await CreateEventAsync(admin, EventBody(responsible.Id, start: Future(11)))).GetProperty("id").GetGuid();
        await ScheduleAsync(admin, scheduled);
        var completed = await SeedEventAsync(responsible.Id, LocalNow().AddDays(-2), status: EventStatus.Completed);
        var cancelled = await SeedEventAsync(responsible.Id, LocalNow().AddDays(5), status: EventStatus.Cancelled);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(EventUrl(draft), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(EventUrl(draft), Ct)).StatusCode);
        foreach (var id in new[] { scheduled, completed, cancelled })
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.DeleteAsync(EventUrl(id), Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(EventUrl(id), Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync(EventUrl(Guid.NewGuid()), Ct)).StatusCode);
    }

    [Fact]
    public async Task CA07_scheduling_several_drafts_returns_a_result_per_event_and_rejects_only_the_one_that_started()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var first = (await CreateEventAsync(admin, EventBody(responsible.Id, "A"))).GetProperty("id").GetGuid();
        var second = (await CreateEventAsync(admin, EventBody(responsible.Id, "B", start: Future(11)))).GetProperty("id").GetGuid();
        var started = await SeedEventAsync(responsible.Id, LocalNow().AddDays(-1), status: EventStatus.Draft);
        var unknown = Guid.NewGuid();

        var outcome = await ScheduleAsync(admin, first, started, unknown, second);

        Assert.Equal("scheduled", outcome.Of(first).GetProperty("status").GetString());
        Assert.Equal("not-published", outcome.Of(first).GetProperty("outlook").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, outcome.Of(started).GetProperty("status").ValueKind);
        Assert.False(string.IsNullOrEmpty(outcome.Of(started).GetProperty("error").GetString()));
        Assert.False(string.IsNullOrEmpty(outcome.Of(unknown).GetProperty("error").GetString()));
        Assert.Equal("scheduled", outcome.Of(second).GetProperty("status").GetString());
        Assert.Equal("scheduled", (await GetEventAsync(admin, first)).GetProperty("status").GetString());
        Assert.Equal("draft", (await GetEventAsync(admin, started)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task CA07_more_than_fifty_events_or_none_at_all_answer_400()
    {
        var admin = await AdminAsync();

        var tooMany = await admin.PostAsJsonAsync(Url("/api/v1/agenda-events/schedule"), new { ids = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()) }, Ct);
        var none = await admin.PostAsJsonAsync(Url("/api/v1/agenda-events/schedule"), new { ids = Array.Empty<Guid>() }, Ct);
        var fifty = await admin.PostAsJsonAsync(Url("/api/v1/agenda-events/schedule"), new { ids = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()) }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fifty.StatusCode);
    }

    [Fact]
    public async Task CA08_cancelling_requires_a_reason_and_closes_the_event()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var id = (await CreateEventAsync(admin, EventBody(responsible.Id))).GetProperty("id").GetGuid();
        var cancel = Url($"/api/v1/agenda-events/{id}/cancel");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(cancel, new { reason = "Draft" }, Ct)).StatusCode);
        await ScheduleAsync(admin, id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(cancel, new { reason = "  " }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(cancel, new { reason = new string('a', 501) }, Ct)).StatusCode);

        var response = await admin.PostAsJsonAsync(cancel, new { reason = "Se pospone por el cierre contable" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("cancelled", json.GetProperty("status").GetString());
        Assert.Equal("Se pospone por el cierre contable", json.GetProperty("cancelReason").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(cancel, new { reason = "otra vez" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.SendAsync(Put(EventUrl(id), EventBody(responsible.Id), json.GetProperty("revision").GetInt32()), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync(Url($"/api/v1/agenda-events/{Guid.NewGuid()}/cancel"), new { reason = "x" }, Ct)).StatusCode);
    }

    [Fact]
    public async Task CA09_the_execution_is_recorded_with_result_notes_and_links_and_then_only_those_can_change()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var id = await SeedEventAsync(responsible.Id, LocalNow().AddDays(-2), status: EventStatus.Scheduled);
        var completion = Url($"/api/v1/agenda-events/{id}/completion");

        var response = await admin.SendAsync(
            Put(completion, new { result = "partial", notes = "  Faltó un servidor  ", evidenceLinks = new[] { new { label = "Informe", url = "https://evidencia.example.com/r1" } } }, null), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("completed", json.GetProperty("status").GetString());
        var record = json.GetProperty("completion");
        Assert.Equal("partial", record.GetProperty("result").GetString());
        Assert.Equal("Faltó un servidor", record.GetProperty("notes").GetString());
        Assert.Equal("https://evidencia.example.com/r1", record.GetProperty("evidenceLinks")[0].GetProperty("url").GetString());
        Assert.True(Math.Abs((record.GetProperty("executedAt").GetDateTimeOffset() - DateTimeOffset.UtcNow).TotalMinutes) < 5);

        var corrected = await admin.SendAsync(
            Put(completion, new { result = "partial", notes = "Corregida", evidenceLinks = new[] { new { label = "Acta", url = "https://example.com/acta" } } }, null), Ct);
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        Assert.Equal("Corregida", (await ReadJsonAsync(corrected)).GetProperty("completion").GetProperty("notes").GetString());

        var changedResult = await admin.SendAsync(Put(completion, new { result = "failed", notes = "x" }, null), Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changedResult.StatusCode);
    }

    [Theory]
    [InlineData("ftp://example.com/x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://usuario:clave@example.com/x")]
    public async Task CA09_links_that_are_not_http_or_have_credentials_are_rejected(string url)
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var id = await SeedEventAsync(responsible.Id, LocalNow().AddDays(-2));

        var response = await admin.SendAsync(
            Put(Url($"/api/v1/agenda-events/{id}/completion"), new { result = "succeeded", evidenceLinks = new[] { new { label = "Mala", url } } }, null), Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("scheduled", (await GetEventAsync(admin, id)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task CA09_the_execution_cannot_be_recorded_before_the_scheduled_start_or_for_a_draft()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var future = (await CreateEventAsync(admin, EventBody(responsible.Id))).GetProperty("id").GetGuid();
        var body = new { result = "succeeded" };

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.SendAsync(Put(Url($"/api/v1/agenda-events/{future}/completion"), body, null), Ct)).StatusCode);
        await ScheduleAsync(admin, future);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.SendAsync(Put(Url($"/api/v1/agenda-events/{future}/completion"), body, null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.SendAsync(Put(Url($"/api/v1/agenda-events/{future}/completion"), new { result = "great" }, null), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.SendAsync(Put(Url($"/api/v1/agenda-events/{Guid.NewGuid()}/completion"), body, null), Ct)).StatusCode);
    }

    [Fact]
    public async Task CA11_the_warnings_of_freezes_and_impact_are_returned_and_never_prevent_saving()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var freeze = await CreateEventAsync(admin, EventBody(responsible.Id, "Cierre contable", FreezeTypeId, Future(20, 0), Future(21, 0)));
        await ScheduleAsync(admin, freeze.GetProperty("id").GetGuid());

        var overlapping = await admin.PostAsJsonAsync(Events, EventBody(responsible.Id, "Parches en cierre", start: Future(20, 9), end: Future(20, 10)), Ct);

        Assert.Equal(HttpStatusCode.Created, overlapping.StatusCode);
        var created = await ReadJsonAsync(overlapping);
        var warning = Assert.Single(WarningsAbout(created, freeze.GetProperty("id").GetGuid()));
        Assert.Equal("freeze-overlap", warning.GetProperty("kind").GetString());
        Assert.Equal("Cierre contable", warning.GetProperty("title").GetString());

        var first = await CreateEventAsync(admin, EventBody(responsible.Id, "Reinicio", impact: "outage", start: Future(30, 9), end: Future(30, 11)));
        var firstId = first.GetProperty("id").GetGuid();
        var second = await CreateEventAsync(admin, EventBody(responsible.Id, "Parches", impact: "degradation", start: Future(30, 10), end: Future(30, 12)));
        var secondId = second.GetProperty("id").GetGuid();
        Assert.Equal("impact-overlap", Assert.Single(WarningsAbout(second, firstId)).GetProperty("kind").GetString());
        var detail = await GetEventAsync(admin, firstId);
        Assert.Equal("impact-overlap", Assert.Single(WarningsAbout(detail, secondId)).GetProperty("kind").GetString());

        var edit = await admin.SendAsync(Put(EventUrl(secondId), EventBody(responsible.Id, "Parches", impact: "degradation", start: Future(30, 10), end: Future(30, 12)), 1), Ct);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Single(WarningsAbout(await ReadJsonAsync(edit), firstId));

        var moved = await admin.SendAsync(Put(EventUrl(secondId), EventBody(responsible.Id, "Parches", impact: "degradation", start: Future(31, 10), end: Future(31, 12)), 2), Ct);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Empty(WarningsAbout(await ReadJsonAsync(moved), firstId));
    }

    [Fact]
    public async Task RF14_the_detail_resolves_who_created_and_changed_the_event()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var created = await CreateEventAsync(admin, EventBody(responsible.Id));
        var id = created.GetProperty("id").GetGuid();

        var detail = await GetEventAsync(admin, id);
        Assert.StartsWith("Usuario ", detail.GetProperty("createdBy").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("updatedBy").ValueKind);

        await admin.SendAsync(Put(EventUrl(id), EventBody(responsible.Id, "Cambiada"), 1), Ct);
        var changed = await GetEventAsync(admin, id);
        Assert.StartsWith("Usuario ", changed.GetProperty("updatedBy").GetString(), StringComparison.Ordinal);
        Assert.NotEqual(JsonValueKind.Null, changed.GetProperty("updatedAt").ValueKind);
    }

    private async Task SetActiveAsync(Guid userId, bool isActive)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Itmas3.Infrastructure.Persistence.AppDbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == userId, Ct);
        user.SetActive(isActive);
        await db.SaveChangesAsync(Ct);
        await ApiFactory.BumpAuthorizationVersionAsync(db);
    }
}

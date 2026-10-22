using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Itmas3.IntegrationTests.Infrastructure;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>SPEC-0006 CA-1: the catalogue of activity types and the migration that seeds it.</summary>
public sealed class AgendaTypesTests(PostgreSqlFixture database) : AgendaTestBase(database)
{
    private static readonly string[] SeededNames =
    [
        "Mantenimiento y parches de sistema operativo", "Pruebas de vulnerabilidad", "Prueba de penetración", "Auditoría de seguridad",
        "Hardening", "Rotación de claves y secretos", "Renovación de certificados TLS", "Pruebas de respaldo y restauración",
        "Revisión de accesos", "Simulacro de continuidad y recuperación", "Congelamiento de cambios", "Otro",
    ];

    private async Task<JsonElement> ListTypesAsync(HttpClient client) =>
        (await ReadJsonAsync(await client.GetAsync(Types, Ct))).GetProperty("items");

    private static object TypeBody(string name, int duration = 90, int reminder = 2880, bool isFreeze = false, bool? isActive = null) =>
        new { name, description = "Descripción de prueba", defaultDurationMinutes = duration, defaultReminderMinutes = reminder, isFreeze, isActive };

    [Fact]
    public async Task CA01_the_catalogue_is_seeded_with_the_types_of_annex_a()
    {
        var admin = await AdminAsync();

        var items = await ListTypesAsync(admin);

        // Other tests of the run add their own types to the shared database: the seeded ones must be there, with fixed ids.
        var names = items.EnumerateArray().Select(item => item.GetProperty("name").GetString()!).ToList();
        Assert.Empty(SeededNames.Except(names));
        var seededIds = Enumerable.Range(1, 12).Select(number => Guid.Parse($"019ba5c0-0000-7000-8000-{number:x12}")).ToList();
        var ids = items.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();
        Assert.Empty(seededIds.Except(ids));
        var freeze = items.EnumerateArray().Single(item => item.GetProperty("name").GetString() == "Congelamiento de cambios");
        Assert.True(freeze.GetProperty("isFreeze").GetBoolean());
        Assert.Equal(1440, freeze.GetProperty("defaultDurationMinutes").GetInt32());
        var patching = items.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == PatchingTypeId);
        Assert.Equal(60, patching.GetProperty("defaultDurationMinutes").GetInt32());
        Assert.Equal(1440, patching.GetProperty("defaultReminderMinutes").GetInt32());
        Assert.True(patching.GetProperty("isActive").GetBoolean());
        Assert.False(patching.GetProperty("isFreeze").GetBoolean());
        Assert.Equal(freeze.GetProperty("id").GetGuid(), FreezeTypeId);
    }

    [Fact]
    public async Task CA01_a_type_is_created_modified_and_deactivated_with_optimistic_concurrency()
    {
        var admin = await AdminAsync();
        var name = Unique("Tipo");

        var created = await admin.PostAsJsonAsync(Types, TypeBody(name), Ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("\"1\"", created.Headers.ETag?.Tag);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var renamed = Unique("Tipo renombrado");
        var updated = await admin.SendAsync(Put(Url($"/api/v1/agenda-event-types/{id}"), TypeBody(renamed, 30, 10, isFreeze: true), 1), Ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("\"2\"", updated.Headers.ETag?.Tag);
        var json = await ReadJsonAsync(updated);
        Assert.Equal(renamed, json.GetProperty("name").GetString());
        Assert.True(json.GetProperty("isFreeze").GetBoolean());

        var stale = await admin.SendAsync(Put(Url($"/api/v1/agenda-event-types/{id}"), TypeBody(renamed), 1), Ct);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);

        var missing = await admin.SendAsync(Put(Url($"/api/v1/agenda-event-types/{id}"), TypeBody(renamed), null), Ct);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);

        var deactivated = await admin.SendAsync(Put(Url($"/api/v1/agenda-event-types/{id}"), TypeBody(renamed, 30, 10, true, isActive: false), 2), Ct);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.False((await ReadJsonAsync(deactivated)).GetProperty("isActive").GetBoolean());

        var notFound = await admin.SendAsync(Put(Url($"/api/v1/agenda-event-types/{Guid.NewGuid()}"), TypeBody("x"), 1), Ct);
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task CA01_a_repeated_name_is_a_business_error_ignoring_case_on_create_and_on_rename()
    {
        var admin = await AdminAsync();
        var name = Unique("Único");
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Types, TypeBody(name), Ct)).StatusCode);

        var repeated = await admin.PostAsJsonAsync(Types, TypeBody(name.ToUpperInvariant()), Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, repeated.StatusCode);

        var seeded = await admin.PostAsJsonAsync(Types, TypeBody("hardening"), Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, seeded.StatusCode);

        var other = await ReadJsonAsync(await admin.PostAsJsonAsync(Types, TypeBody(Unique("Otro tipo")), Ct));
        var rename = await admin.SendAsync(
            Put(Url($"/api/v1/agenda-event-types/{other.GetProperty("id").GetGuid()}"), TypeBody(name.ToLowerInvariant()), 1), Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rename.StatusCode);
    }

    [Theory]
    [InlineData("", 60, 60)]
    [InlineData("Tipo", 0, 60)]
    [InlineData("Tipo", 86_401, 60)]
    [InlineData("Tipo", 60, -1)]
    [InlineData("Tipo", 60, 40_321)]
    public async Task CA01_the_name_the_duration_and_the_reminder_are_validated(string name, int duration, int reminder)
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync(Types, TypeBody(name.Length == 0 ? name : Unique(name), duration, reminder), Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CA01_an_inactive_type_cannot_be_assigned_to_a_new_event_but_existing_events_keep_it()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var type = await ReadJsonAsync(await admin.PostAsJsonAsync(Types, TypeBody(Unique("Temporal"), 60, 60), Ct));
        var typeId = type.GetProperty("id").GetGuid();
        var existing = await CreateEventAsync(admin, EventBody(responsible.Id, typeId: typeId));
        var existingId = existing.GetProperty("id").GetGuid();

        var deactivate = await admin.SendAsync(Put(Url($"/api/v1/agenda-event-types/{typeId}"), TypeBody(type.GetProperty("name").GetString()!, 60, 60, isActive: false), 1), Ct);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var rejected = await admin.PostAsJsonAsync(Events, EventBody(responsible.Id, typeId: typeId), Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);

        var current = await GetEventAsync(admin, existingId);
        Assert.Equal(typeId, current.GetProperty("type").GetProperty("id").GetGuid());
        var edit = await admin.SendAsync(Put(EventUrl(existingId), EventBody(responsible.Id, "Título editado", typeId), current.GetProperty("revision").GetInt32()), Ct);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
    }

    [Fact]
    public async Task CA01_unknown_properties_are_rejected()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsync(Types, Json("""{"name":"X","defaultDurationMinutes":60,"defaultReminderMinutes":0,"isFreeze":false,"color":"red"}"""), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

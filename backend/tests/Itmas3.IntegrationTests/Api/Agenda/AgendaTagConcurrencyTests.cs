using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>
/// SPEC-0006 RN-15 and CA-25: two people using the same new tag at the same time is not an error. The tag is created once, the
/// other request reuses it and the spelling of the first one stays. Before the retry of the unit of work the loser got 409.
/// </summary>
public sealed class AgendaTagConcurrencyTests(PostgreSqlFixture database) : AgendaTestBase(database)
{
    private static string[] TagsOf(JsonElement json) => [.. json.GetProperty("tags").EnumerateArray().Select(item => item.GetString()!)];

    private async Task<int> CountTagsAsync(string name)
    {
        var lowered = name.ToLowerInvariant();
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.AgendaTags.CountAsync(tag => tag.Name.ToLower() == lowered, Ct);
    }

    /// <summary>Waits until a backend waits for a lock held by the backend <paramref name="holderPid"/>.</summary>
    private async Task WaitUntilBlockedByAsync(int holderPid, Task request)
    {
        await using var connection = new NpgsqlConnection(Database.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE @holder = ANY(pg_blocking_pids(pid))", connection);
        command.Parameters.AddWithValue("holder", holderPid);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while ((long)(await command.ExecuteScalarAsync(Ct))! == 0)
        {
            Assert.False(request.IsCompleted, "The request finished without waiting for the tag held by the other transaction.");
            Assert.True(DateTime.UtcNow < deadline, "The request never waited for the tag held by the other transaction.");
            await Task.Delay(25, Ct);
        }
    }

    [Fact]
    public async Task CA25_a_series_that_loses_the_race_for_a_new_tag_reuses_it_and_keeps_the_first_spelling()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..8];
        var firstSpelling = $"Carrera-{token}";

        // Another transaction has inserted the tag but not committed yet: the series cannot see it, tries to create it as well
        // and waits for it. When the other one commits, the unique index rejects the insert of the series (the race).
        await using var holder = new NpgsqlConnection(Database.AdminConnectionString);
        await holder.OpenAsync(Ct);
        await using var transaction = await holder.BeginTransactionAsync(Ct);
        await using (var insert = new NpgsqlCommand("INSERT INTO app.agenda_tags (id, name, created_at, created_by) VALUES (@id, @name, now(), @by)", holder, transaction))
        {
            insert.Parameters.AddWithValue("id", Guid.CreateVersion7());
            insert.Parameters.AddWithValue("name", firstSpelling);
            insert.Parameters.AddWithValue("by", responsible.Id);
            await insert.ExecuteNonQueryAsync(Ct);
        }

        int holderPid;
        await using (var pid = new NpgsqlCommand("SELECT pg_backend_pid()", holder, transaction))
        {
            holderPid = (int)(await pid.ExecuteScalarAsync(Ct))!;
        }

        var body = new
        {
            @event = EventBody(responsible.Id, tags: [firstSpelling.ToUpperInvariant()]),
            rule = new { unit = "week", interval = 1, anchor = (object?)null, endsOn = (string?)null, count = 2 },
        };
        var request = admin.PostAsJsonAsync(Series, body, Ct);
        await WaitUntilBlockedByAsync(holderPid, request);

        await transaction.CommitAsync(Ct);
        var response = await request;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ids = (await ReadJsonAsync(response)).GetProperty("eventIds").EnumerateArray().Select(item => item.GetGuid()).ToList();
        Assert.Equal(2, ids.Count);
        foreach (var id in ids)
        {
            Assert.Equal([firstSpelling], TagsOf(await GetEventAsync(admin, id)));
        }

        Assert.Equal(1, await CountTagsAsync(firstSpelling));
    }

    [Fact]
    public async Task CA25_two_spellings_that_the_database_sees_as_one_tag_are_one_tag_and_never_answer_409()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..8];
        var lower = $"istanbul-{token}";
        var dotted = $"\u0130stanbul-{token}";

        var first = await CreateEventAsync(admin, EventBody(responsible.Id, tags: [lower]));
        var second = await CreateEventAsync(admin, EventBody(responsible.Id, tags: [dotted]));

        Assert.Equal([lower], TagsOf(first));
        Assert.Equal([lower], TagsOf(second));
        Assert.Equal(1, await CountTagsAsync(lower));
    }

    [Fact]
    public async Task CA25_events_created_at_the_same_time_with_the_same_new_tag_all_answer_201_and_create_the_tag_once()
    {
        var admin = await AdminAsync();
        var responsible = await UserAsync();
        var token = Guid.NewGuid().ToString("N")[..8];
        var spellings = new[] { $"Simultanea-{token}", $"SIMULTANEA-{token}", $"simultanea-{token}" };

        var gate = new TaskCompletionSource();
        var requests = Enumerable.Range(0, 9).Select(index => Task.Run(async () =>
        {
            await gate.Task;
            return await admin.PostAsJsonAsync(Events, EventBody(responsible.Id, tags: [spellings[index % spellings.Length]]), Ct);
        })).ToList();
        gate.SetResult();
        var responses = await Task.WhenAll(requests);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var stored = new HashSet<string>();
        foreach (var response in responses)
        {
            var tags = TagsOf(await GetEventAsync(admin, (await ReadJsonAsync(response)).GetProperty("id").GetGuid()));
            stored.Add(Assert.Single(tags));
        }

        Assert.Single(stored);
        Assert.Contains(stored.Single(), spellings);
        Assert.Equal(1, await CountTagsAsync(spellings[0]));
    }
}

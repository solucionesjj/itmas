using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Events;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Domain.Agenda;
using Itmas3.Infrastructure.Agenda.Graph;
using Itmas3.UnitTests.Application.Agenda;

namespace Itmas3.UnitTests.Infrastructure.Agenda.Graph;

/// <summary>
/// SPEC-0006 CA-14 to CA-16 at the HTTP level (ADR-0016): what the adapter sends to Graph and how it normalizes what comes back,
/// with a simulated <see cref="HttpMessageHandler"/>. Nothing calls Microsoft (constitution §9.4).
/// </summary>
public sealed class GraphOutlookCalendarGatewayTests
{
    private const string CreatedId = "AAMkAGI2-created=";
    private const string EventsUrl = "https://graph.microsoft.com/v1.0/users/agenda%40example.com/calendar/events";

    private readonly GraphGatewayHarness _graph = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<OutlookGatewayResult> PublishAsync(OutlookEventData? data = null, string? outlookEventId = null) =>
        _graph.Gateway.PublishAsync(data ?? GraphGatewayHarness.Data(), outlookEventId, Ct);

    // ---- CA-14: what is created -------------------------------------------------------------------------------------

    [Fact]
    public async Task CA14_creating_posts_the_event_to_the_calendar_of_the_mailbox_with_the_application_token()
    {
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());

        var result = await PublishAsync();

        var request = Assert.Single(_graph.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(EventsUrl, request.Uri.AbsoluteUri);
        Assert.Equal($"Bearer {StubTokenProvider.Token}", request.Authorization);
        Assert.Equal("application/json", request.MediaType);
        Assert.True(result.Succeeded);
        Assert.Equal(CreatedId, result.OutlookEventId);
        Assert.Equal("https://outlook.office365.com/owa/?itemid=abc&exvsurl=1", result.WebLink);
    }

    [Fact]
    public async Task CA14_the_event_carries_subject_plain_text_body_category_reminder_invitees_and_the_transaction_id()
    {
        var data = GraphGatewayHarness.Data();
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());

        await PublishAsync(data);

        var json = Assert.Single(_graph.Handler.Requests).Json;
        Assert.Equal("[Mantenimiento y parches] Parches <b>de</b> SO", json.GetProperty("subject").GetString());
        Assert.Equal("text", json.GetProperty("body").GetProperty("contentType").GetString());
        Assert.Equal(data.BodyText, json.GetProperty("body").GetProperty("content").GetString());
        Assert.Equal(["Mantenimiento y parches"], json.GetProperty("categories").EnumerateArray().Select(c => c.GetString()));
        Assert.True(json.GetProperty("isReminderOn").GetBoolean());
        Assert.Equal(1440, json.GetProperty("reminderMinutesBeforeStart").GetInt32());
        Assert.Equal("Centro de datos", json.GetProperty("location").GetProperty("displayName").GetString());
        Assert.False(json.GetProperty("isAllDay").GetBoolean());
        Assert.Equal(data.EventId.ToString("D"), json.GetProperty("transactionId").GetString());

        var attendees = json.GetProperty("attendees").EnumerateArray()
            .Select(a => (a.GetProperty("emailAddress").GetProperty("address").GetString(), a.GetProperty("type").GetString()))
            .ToArray();
        Assert.Equal(
            [("ana.perez@example.com", "required"), ("luis@example.com", "required"), ("equipo@example.com", "optional")],
            attendees);
    }

    [Fact]
    public async Task CA14_start_and_end_travel_as_local_time_with_the_zone_of_the_event()
    {
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());

        await PublishAsync();

        var json = Assert.Single(_graph.Handler.Requests).Json;
        Assert.Equal("2026-10-24T09:00:00.0000000", json.GetProperty("start").GetProperty("dateTime").GetString());
        Assert.Equal("America/Bogota", json.GetProperty("start").GetProperty("timeZone").GetString());
        Assert.Equal("2026-10-24T11:00:00.0000000", json.GetProperty("end").GetProperty("dateTime").GetString());
        Assert.Equal("America/Bogota", json.GetProperty("end").GetProperty("timeZone").GetString());
    }

    [Fact]
    public async Task CA14_only_the_properties_of_rf16_are_sent_so_the_tags_of_itmas_never_travel()
    {
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());

        await PublishAsync();

        var names = Assert.Single(_graph.Handler.Requests).Json.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);
        Assert.Equal(
            ["attendees", "body", "categories", "end", "isAllDay", "isReminderOn", "location", "reminderMinutesBeforeStart", "start", "subject", "transactionId"],
            names);
    }

    [Fact]
    public async Task CA22_markup_in_the_title_and_the_description_goes_out_literally_as_plain_text()
    {
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());

        await PublishAsync();

        var json = Assert.Single(_graph.Handler.Requests).Json;
        Assert.Contains("<b>de</b>", json.GetProperty("subject").GetString(), StringComparison.Ordinal);
        Assert.Contains("<script>alert(1)</script>", json.GetProperty("body").GetProperty("content").GetString(), StringComparison.Ordinal);
        Assert.Equal("text", json.GetProperty("body").GetProperty("contentType").GetString());
    }

    [Fact]
    public async Task CA14_without_a_location_the_location_is_sent_empty_so_that_an_update_clears_it()
    {
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());

        await PublishAsync(GraphGatewayHarness.Data(location: null));

        Assert.Equal(string.Empty, Assert.Single(_graph.Handler.Requests).Json.GetProperty("location").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task CA14_an_event_without_the_team_has_no_optional_invitee()
    {
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());

        await PublishAsync(GraphGatewayHarness.Data(optional: []));

        var types = Assert.Single(_graph.Handler.Requests).Json.GetProperty("attendees").EnumerateArray().Select(a => a.GetProperty("type").GetString());
        Assert.All(types, type => Assert.Equal("required", type));
    }

    [Fact]
    public async Task P24_an_all_day_event_goes_out_as_whole_days_with_an_exclusive_end()
    {
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());
        var data = GraphGatewayHarness.Data(isAllDay: true, start: new DateTime(2026, 11, 24), end: new DateTime(2026, 11, 26));

        await PublishAsync(data);

        var json = Assert.Single(_graph.Handler.Requests).Json;
        Assert.True(json.GetProperty("isAllDay").GetBoolean());
        Assert.Equal("2026-11-24T00:00:00.0000000", json.GetProperty("start").GetProperty("dateTime").GetString());
        Assert.Equal("2026-11-26T00:00:00.0000000", json.GetProperty("end").GetProperty("dateTime").GetString());
    }

    [Fact]
    public async Task P24_an_all_day_event_is_forced_to_midnight_and_to_end_after_it_starts()
    {
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());
        var data = GraphGatewayHarness.Data(isAllDay: true, start: new DateTime(2026, 11, 24, 7, 30, 0), end: new DateTime(2026, 11, 24, 22, 0, 0));

        await PublishAsync(data);

        var json = Assert.Single(_graph.Handler.Requests).Json;
        Assert.Equal("2026-11-24T00:00:00.0000000", json.GetProperty("start").GetProperty("dateTime").GetString());
        Assert.Equal("2026-11-25T00:00:00.0000000", json.GetProperty("end").GetProperty("dateTime").GetString());
    }

    [Fact]
    public async Task The_adapter_never_depends_on_the_culture_of_the_process()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());

            await PublishAsync();

            Assert.Equal("2026-10-24T09:00:00.0000000", Assert.Single(_graph.Handler.Requests).Json.GetProperty("start").GetProperty("dateTime").GetString());
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    // ---- The answer of Graph ----------------------------------------------------------------------------------------

    [Fact]
    public async Task A_created_event_without_an_id_in_the_answer_is_rejected()
    {
        _graph.Handler.Respond(HttpStatusCode.Created, "{\"subject\":\"x\"}");

        var result = await PublishAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(OutlookErrorCodes.Rejected, result.ErrorCode);
    }

    [Theory]
    [InlineData("../../me")]
    [InlineData("AAMk/..%2F..")]
    [InlineData("AAMk?x=1")]
    [InlineData("AAMk#x")]
    [InlineData("AAMk id")]
    [InlineData("..")]
    public async Task An_id_that_could_change_a_path_is_never_accepted_from_graph_nor_used_in_a_request(string id)
    {
        _graph.Handler.Respond(HttpStatusCode.Created, JsonSerializer.Serialize(new { id }));

        var created = await PublishAsync();
        var updated = await PublishAsync(outlookEventId: id);
        var cancelled = await _graph.Gateway.CancelAsync(id, "motivo", Ct);

        Assert.Equal(OutlookErrorCodes.Rejected, created.ErrorCode);
        Assert.Equal(OutlookErrorCodes.Rejected, updated.ErrorCode);
        Assert.Equal(OutlookErrorCodes.Rejected, cancelled.ErrorCode);
        Assert.Single(_graph.Handler.Requests); // only the creation went out
    }

    [Fact]
    public async Task An_id_longer_than_its_column_is_rejected()
    {
        _graph.Handler.Respond(HttpStatusCode.Created, JsonSerializer.Serialize(new { id = new string('A', 301) }));

        Assert.Equal(OutlookErrorCodes.Rejected, (await PublishAsync()).ErrorCode);
    }

    [Theory]
    [InlineData("http://outlook.office365.com/owa/?itemid=abc")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:pass@outlook.office365.com/owa")]
    [InlineData("/owa/?itemid=abc")]
    [InlineData("not a link")]
    public async Task A_link_that_is_not_a_plain_https_address_is_not_kept_but_the_event_is_still_published(string link)
    {
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson(webLink: link));

        var result = await PublishAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(CreatedId, result.OutlookEventId);
        Assert.Null(result.WebLink);
    }

    [Fact]
    public async Task A_link_longer_than_its_column_is_not_kept()
    {
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson(webLink: "https://outlook.office365.com/owa/?itemid=" + new string('a', 1000)));

        var result = await PublishAsync();

        Assert.True(result.Succeeded);
        Assert.Null(result.WebLink);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("")]
    public async Task An_answer_that_is_not_an_event_is_rejected_without_failing(string body)
    {
        _graph.Handler.Respond(HttpStatusCode.Created, body);

        Assert.Equal(OutlookErrorCodes.Rejected, (await PublishAsync()).ErrorCode);
    }

    [Fact]
    public async Task An_answer_larger_than_the_limit_is_rejected_without_being_read_in_full()
    {
        _graph.Handler.Respond(HttpStatusCode.Created, "{\"id\":\"" + new string('A', 2_000_000) + "\"}");

        Assert.Equal(OutlookErrorCodes.Rejected, (await PublishAsync()).ErrorCode);
    }

    [Fact]
    public async Task Every_request_goes_to_graph_over_https_and_never_follows_a_url_from_the_data()
    {
        _graph.Handler
            .Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson())
            .Respond(HttpStatusCode.OK, GraphGatewayHarness.CreatedJson())
            .Respond(HttpStatusCode.Accepted);
        var data = GraphGatewayHarness.Data() with { Subject = "https://evil.example/x", BodyText = "http://169.254.169.254/latest/meta-data", Location = "https://evil.example" };

        await PublishAsync(data);
        await PublishAsync(data, CreatedId);
        await _graph.Gateway.CancelAsync(CreatedId, "https://evil.example", Ct);

        Assert.Equal(3, _graph.Handler.Requests.Count);
        Assert.All(_graph.Handler.Requests, request =>
        {
            Assert.Equal("https", request.Uri.Scheme);
            Assert.Equal("graph.microsoft.com", request.Uri.Host);
            Assert.StartsWith("/v1.0/users/agenda%40example.com/calendar/events", request.Uri.AbsolutePath, StringComparison.Ordinal);
        });
    }

    // ---- CA-15: update and cancel -----------------------------------------------------------------------------------

    [Fact]
    public async Task CA15_updating_patches_the_saved_event_with_the_whole_state_and_without_a_transaction_id()
    {
        _graph.Handler.Respond(HttpStatusCode.OK, GraphGatewayHarness.CreatedJson(id: CreatedId));
        var data = GraphGatewayHarness.Data();

        var result = await PublishAsync(data, CreatedId);

        var request = Assert.Single(_graph.Handler.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal($"{EventsUrl}/AAMkAGI2-created%3D", request.Uri.AbsoluteUri);
        Assert.Equal($"Bearer {StubTokenProvider.Token}", request.Authorization);
        var json = request.Json;
        Assert.False(json.TryGetProperty("transactionId", out _), "Graph does not let the transaction id change after the creation.");
        Assert.Equal(data.Subject, json.GetProperty("subject").GetString());
        Assert.Equal(3, json.GetProperty("attendees").GetArrayLength());
        Assert.Equal("text", json.GetProperty("body").GetProperty("contentType").GetString());
        Assert.True(result.Succeeded);
        Assert.Equal(CreatedId, result.OutlookEventId);
        Assert.NotNull(result.WebLink);
    }

    [Fact]
    public async Task CA15_an_update_without_a_body_in_the_answer_keeps_the_saved_id()
    {
        _graph.Handler.Respond(HttpStatusCode.NoContent);

        var result = await PublishAsync(outlookEventId: CreatedId);

        Assert.True(result.Succeeded);
        Assert.Equal(CreatedId, result.OutlookEventId);
    }

    [Fact]
    public async Task CA15_cancelling_posts_the_cancellation_with_the_reason_as_the_comment()
    {
        _graph.Handler.Respond(HttpStatusCode.Accepted);

        var result = await _graph.Gateway.CancelAsync(CreatedId, "Se pospone por una ventana de cambios <b>mayor</b>", Ct);

        var request = Assert.Single(_graph.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"{EventsUrl}/AAMkAGI2-created%3D/cancel", request.Uri.AbsoluteUri);
        Assert.Equal($"Bearer {StubTokenProvider.Token}", request.Authorization);
        Assert.Equal(["comment"], request.Json.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Se pospone por una ventana de cambios <b>mayor</b>", request.Json.GetProperty("comment").GetString());
        Assert.True(result.Succeeded);
        Assert.Equal(CreatedId, result.OutlookEventId);
    }

    [Fact]
    public async Task CA15_cancelling_an_event_that_no_longer_exists_counts_as_cancelled()
    {
        _graph.Handler.Respond(HttpStatusCode.NotFound);

        var result = await _graph.Gateway.CancelAsync(CreatedId, "motivo", Ct);

        Assert.True(result.Succeeded);
        Assert.Equal(CreatedId, result.OutlookEventId);
    }

    // ---- CA-16: recreation, idempotency ----------------------------------------------------------------------------

    [Fact]
    public async Task CA16_an_update_that_finds_no_event_creates_it_again_with_a_new_transaction_id()
    {
        var data = GraphGatewayHarness.Data();
        _graph.Handler
            .Respond(HttpStatusCode.NotFound)
            .Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson(id: "AAMkAGI2-recreated="));

        var result = await PublishAsync(data, "AAMkAGI2-gone=");

        Assert.Equal(2, _graph.Handler.Requests.Count);
        Assert.Equal(HttpMethod.Patch, _graph.Handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, _graph.Handler.Requests[1].Method);
        Assert.Equal(EventsUrl, _graph.Handler.Requests[1].Uri.AbsoluteUri);
        var transactionId = _graph.Handler.Requests[1].Json.GetProperty("transactionId").GetString();
        Assert.NotEqual(data.EventId.ToString("D"), transactionId); // Graph may remember the one of the removed event
        Assert.True(Guid.TryParse(transactionId, out _));
        Assert.True(result.Succeeded);
        Assert.Equal("AAMkAGI2-recreated=", result.OutlookEventId);
    }

    [Fact]
    public async Task CA16_recreating_twice_after_a_timeout_uses_the_same_transaction_id_so_nothing_is_duplicated()
    {
        var data = GraphGatewayHarness.Data();
        _graph.Handler
            .Respond(HttpStatusCode.NotFound)
            .Run((_, _) => throw new TaskCanceledException("simulated", new TimeoutException()))
            .Respond(HttpStatusCode.NotFound)
            .Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());

        var first = await PublishAsync(data, "AAMkAGI2-gone=");
        var second = await PublishAsync(data, "AAMkAGI2-gone=");

        Assert.Equal(OutlookErrorCodes.Timeout, first.ErrorCode);
        Assert.True(second.Succeeded);
        var posts = _graph.Handler.Requests.Where(r => r.Method == HttpMethod.Post).Select(r => r.Json.GetProperty("transactionId").GetString()).ToArray();
        Assert.Equal(2, posts.Length);
        Assert.Equal(posts[0], posts[1]);
    }

    [Fact]
    public async Task CA16_a_first_creation_uses_the_id_of_the_agenda_event_so_a_retry_after_a_timeout_never_duplicates_it()
    {
        var data = GraphGatewayHarness.Data();
        _graph.Handler
            .Run((_, _) => throw new TaskCanceledException("simulated", new TimeoutException()))
            .Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());

        var first = await PublishAsync(data);
        var retry = await PublishAsync(data);

        Assert.Equal(OutlookErrorCodes.Timeout, first.ErrorCode);
        Assert.True(retry.Succeeded);
        Assert.All(
            _graph.Handler.Requests,
            request => Assert.Equal(data.EventId.ToString("D"), request.Json.GetProperty("transactionId").GetString()));
    }

    [Fact]
    public async Task CA16_if_the_recreation_is_refused_the_failure_is_the_one_of_the_creation()
    {
        _graph.Handler.Respond(HttpStatusCode.NotFound).Respond(HttpStatusCode.Forbidden);

        var result = await PublishAsync(outlookEventId: "AAMkAGI2-gone=");

        Assert.Equal(OutlookErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(2, _graph.Handler.Requests.Count);
    }

    [Fact]
    public async Task CA16_a_missing_mailbox_answers_not_found_on_the_creation_and_nothing_is_recreated_in_a_loop()
    {
        _graph.Handler.Respond(HttpStatusCode.NotFound);

        var result = await PublishAsync();

        Assert.Equal(OutlookErrorCodes.NotFound, result.ErrorCode);
        Assert.Single(_graph.Handler.Requests);
    }

    // ---- CA-16: normalized errors ----------------------------------------------------------------------------------

    public static TheoryData<HttpStatusCode, string> StatusCodes => new()
    {
        { HttpStatusCode.Unauthorized, OutlookErrorCodes.Unauthorized },
        { HttpStatusCode.Forbidden, OutlookErrorCodes.Forbidden },
        { HttpStatusCode.NotFound, OutlookErrorCodes.NotFound },
        { HttpStatusCode.RequestTimeout, OutlookErrorCodes.Timeout },
        { HttpStatusCode.TooManyRequests, OutlookErrorCodes.Throttled },
        { HttpStatusCode.InternalServerError, OutlookErrorCodes.Unavailable },
        { HttpStatusCode.BadGateway, OutlookErrorCodes.Unavailable },
        { HttpStatusCode.ServiceUnavailable, OutlookErrorCodes.Unavailable },
        { HttpStatusCode.GatewayTimeout, OutlookErrorCodes.Unavailable },
        { HttpStatusCode.BadRequest, OutlookErrorCodes.Rejected },
        { HttpStatusCode.Conflict, OutlookErrorCodes.Rejected },
        { HttpStatusCode.UnprocessableEntity, OutlookErrorCodes.Rejected },
        { HttpStatusCode.Found, OutlookErrorCodes.Rejected },
    };

    [Theory]
    [MemberData(nameof(StatusCodes))]
    public async Task CA16_creating_maps_each_status_to_its_normalized_code(HttpStatusCode status, string expected)
    {
        _graph.Handler.Respond(status, "{\"error\":{\"code\":\"ErrorSecretDetail\",\"message\":\"detail that must not leave\"}}");

        var result = await PublishAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.ErrorCode);
        Assert.Null(result.OutlookEventId);
        Assert.Contains(result.ErrorCode!, OutlookErrorCodes.All);
    }

    [Theory]
    [MemberData(nameof(StatusCodes))]
    public async Task CA16_updating_maps_each_status_to_its_normalized_code_and_only_a_404_triggers_the_recreation(HttpStatusCode status, string expected)
    {
        _graph.Handler.Respond(status);
        if (status == HttpStatusCode.NotFound)
        {
            _graph.Handler.Respond(HttpStatusCode.NotFound); // the recreation also finds nothing (for instance, the mailbox)
        }

        var result = await PublishAsync(outlookEventId: CreatedId);

        Assert.Equal(expected, result.ErrorCode);
        Assert.Equal(status == HttpStatusCode.NotFound ? 2 : 1, _graph.Handler.Requests.Count);
    }

    [Theory]
    [MemberData(nameof(StatusCodes))]
    public async Task CA16_cancelling_maps_each_status_to_its_normalized_code_except_a_404_which_is_already_cancelled(HttpStatusCode status, string expected)
    {
        _graph.Handler.Respond(status);

        var result = await _graph.Gateway.CancelAsync(CreatedId, "motivo", Ct);

        if (status == HttpStatusCode.NotFound)
        {
            Assert.True(result.Succeeded);
        }
        else
        {
            Assert.Equal(expected, result.ErrorCode);
        }
    }

    [Fact]
    public async Task CA16_a_401_discards_the_cached_token_so_the_next_call_asks_for_a_new_one()
    {
        _graph.Handler.Respond(HttpStatusCode.Unauthorized);

        await PublishAsync();

        Assert.Equal(1, _graph.Tokens.Invalidations);
    }

    [Fact]
    public async Task CA16_other_failures_keep_the_cached_token()
    {
        _graph.Handler.Respond(HttpStatusCode.Forbidden).Respond(HttpStatusCode.InternalServerError);

        await PublishAsync();
        await PublishAsync();

        Assert.Equal(0, _graph.Tokens.Invalidations);
    }

    [Fact]
    public async Task CA16_a_network_failure_is_graph_unavailable()
    {
        _graph.Handler.Run((_, _) => throw new HttpRequestException("name resolution failed for graph.microsoft.com"));

        var result = await PublishAsync();

        Assert.Equal(OutlookErrorCodes.Unavailable, result.ErrorCode);
        Assert.False(result.Throttled);
    }

    [Fact]
    public async Task CA16_a_connection_cut_while_reading_the_answer_is_graph_unavailable()
    {
        _graph.Handler.Run((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = new ThrowingContent() }));

        Assert.Equal(OutlookErrorCodes.Unavailable, (await PublishAsync()).ErrorCode);
    }

    // ---- CA-16: time limit ------------------------------------------------------------------------------------------

    [Fact]
    public async Task CA16_a_call_that_takes_longer_than_the_limit_is_graph_timeout()
    {
        var graph = new GraphGatewayHarness(TimeSpan.FromMilliseconds(100));
        graph.Handler.Run(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var result = await graph.Gateway.PublishAsync(GraphGatewayHarness.Data(), null, Ct);

        Assert.Equal(OutlookErrorCodes.Timeout, result.ErrorCode);
        Assert.False(result.Throttled);
    }

    [Fact]
    public void CA16_the_time_limit_of_graph_is_ten_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), GraphOutlookSettings.DefaultRequestTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), new GraphOutlookSettings("t", "c", "s", "m@example.com").RequestTimeout);
    }

    [Fact]
    public async Task CA16_the_time_limit_also_covers_the_token()
    {
        var graph = new GraphGatewayHarness(TimeSpan.FromMilliseconds(100));
        var slow = new SlowTokenProvider();
        var gateway = new GraphOutlookCalendarGateway(
            new HttpClient(graph.Handler), slow, new GraphOutlookSettings("t", "c", "s", "m@example.com") { RequestTimeout = TimeSpan.FromMilliseconds(100) },
            graph.Gate, graph.Clock, graph.Logger);

        var result = await gateway.PublishAsync(GraphGatewayHarness.Data(), null, Ct);

        Assert.Equal(OutlookErrorCodes.Timeout, result.ErrorCode);
        Assert.Empty(graph.Handler.Requests);
    }

    [Fact]
    public async Task A_cancellation_requested_by_the_caller_is_not_a_failure_of_graph()
    {
        for (var call = 0; call < 2; call++)
        {
            _graph.Handler.Run(async (_, ct) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return new HttpResponseMessage(HttpStatusCode.Created);
            });
        }

        using var cancelPublish = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _graph.Gateway.PublishAsync(GraphGatewayHarness.Data(), null, cancelPublish.Token));
        using var cancelCancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _graph.Gateway.CancelAsync(CreatedId, "motivo", cancelCancel.Token));
    }

    // ---- RNF-5: throttling ------------------------------------------------------------------------------------------

    [Fact]
    public async Task RNF5_a_429_stops_the_batch_and_closes_the_gate_for_the_time_graph_asked()
    {
        _graph.Handler.Respond(HttpStatusCode.TooManyRequests, null, response => response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(45)));

        var result = await PublishAsync();

        Assert.Equal(OutlookErrorCodes.Throttled, result.ErrorCode);
        Assert.True(result.Throttled);
        Assert.True(_graph.Gate.IsClosed(out var remaining));
        Assert.Equal(TimeSpan.FromSeconds(45), remaining);
    }

    [Fact]
    public async Task RNF5_while_the_gate_is_closed_nothing_is_sent_and_every_call_answers_throttled()
    {
        _graph.Handler
            .Respond(HttpStatusCode.TooManyRequests, null, response => response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(45)))
            .Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());
        await PublishAsync();

        var publish = await PublishAsync();
        var update = await PublishAsync(outlookEventId: CreatedId);
        var cancel = await _graph.Gateway.CancelAsync(CreatedId, "motivo", Ct);

        Assert.Single(_graph.Handler.Requests);
        Assert.All(new[] { publish, update, cancel }, result =>
        {
            Assert.Equal(OutlookErrorCodes.Throttled, result.ErrorCode);
            Assert.True(result.Throttled);
        });
        Assert.Equal(1, _graph.Tokens.Acquisitions); // not even the token is asked for
    }

    [Fact]
    public async Task RNF5_after_the_wait_the_next_call_goes_out_again()
    {
        _graph.Handler
            .Respond(HttpStatusCode.TooManyRequests, null, response => response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(45)))
            .Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());
        await PublishAsync();

        _graph.Clock.Set(AgendaHarness.Now.AddSeconds(44));
        Assert.Equal(OutlookErrorCodes.Throttled, (await PublishAsync()).ErrorCode);
        _graph.Clock.Set(AgendaHarness.Now.AddSeconds(46));
        var result = await PublishAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(2, _graph.Handler.Requests.Count);
        Assert.False(_graph.Gate.IsClosed(out _));
    }

    [Fact]
    public async Task RNF5_retry_after_may_be_a_date()
    {
        _graph.Handler.Respond(
            HttpStatusCode.TooManyRequests, null, response => response.Headers.RetryAfter = new RetryConditionHeaderValue(AgendaHarness.Now.AddSeconds(20)));

        await PublishAsync();

        Assert.True(_graph.Gate.IsClosed(out var remaining));
        Assert.Equal(TimeSpan.FromSeconds(20), remaining);
    }

    [Fact]
    public async Task RNF5_without_retry_after_the_gate_waits_the_default_and_a_huge_retry_after_is_capped()
    {
        _graph.Handler.Respond(HttpStatusCode.TooManyRequests);
        await PublishAsync();
        Assert.True(_graph.Gate.IsClosed(out var withoutHeader));
        Assert.Equal(GraphThrottleGate.DefaultDelay, withoutHeader);

        var other = new GraphGatewayHarness();
        other.Handler.Respond(HttpStatusCode.TooManyRequests, null, response => response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(3)));
        await other.Gateway.PublishAsync(GraphGatewayHarness.Data(), null, Ct);
        Assert.True(other.Gate.IsClosed(out var capped));
        Assert.Equal(GraphThrottleGate.MaxDelay, capped);
    }

    [Fact]
    public async Task RNF5_a_503_that_says_when_to_come_back_stops_the_batch_but_stays_graph_unavailable()
    {
        _graph.Handler.Respond(HttpStatusCode.ServiceUnavailable, null, response => response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(10)));

        var result = await PublishAsync();

        Assert.Equal(OutlookErrorCodes.Unavailable, result.ErrorCode);
        Assert.True(result.Throttled);
        Assert.True(_graph.Gate.IsClosed(out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task RNF5_other_failures_do_not_stop_the_batch(HttpStatusCode status)
    {
        _graph.Handler.Respond(status);

        var result = await PublishAsync();

        Assert.False(result.Throttled);
        Assert.False(_graph.Gate.IsClosed(out _));
    }

    // ---- The token --------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(OutlookErrorCodes.Unauthorized)]
    [InlineData(OutlookErrorCodes.Unavailable)]
    [InlineData(OutlookErrorCodes.Timeout)]
    [InlineData(OutlookErrorCodes.NotConfigured)]
    public async Task A_token_that_cannot_be_obtained_is_reported_with_its_code_and_nothing_is_sent(string code)
    {
        _graph.Tokens.Result = GraphTokenResult.Failure(code);

        var publish = await PublishAsync();
        var cancel = await _graph.Gateway.CancelAsync(CreatedId, "motivo", Ct);

        Assert.Equal(code, publish.ErrorCode);
        Assert.Equal(code, cancel.ErrorCode);
        Assert.Empty(_graph.Handler.Requests);
    }

    [Fact]
    public async Task A_token_request_that_is_throttled_closes_the_gate_and_stops_the_batch()
    {
        _graph.Tokens.Result = GraphTokenResult.Failure(OutlookErrorCodes.Throttled, TimeSpan.FromSeconds(30));

        var result = await PublishAsync();

        Assert.Equal(OutlookErrorCodes.Throttled, result.ErrorCode);
        Assert.True(result.Throttled);
        Assert.True(_graph.Gate.IsClosed(out var remaining));
        Assert.Equal(TimeSpan.FromSeconds(30), remaining);
    }

    [Fact]
    public async Task A_code_outside_the_closed_set_is_normalized_never_passed_on()
    {
        _graph.Tokens.Result = GraphTokenResult.Failure("AADSTS7000215 invalid secret for agenda@example.com");

        var result = await PublishAsync();

        Assert.Equal(OutlookErrorCodes.Unavailable, result.ErrorCode);
    }

    // ---- RNF-8: nothing sensitive is logged -------------------------------------------------------------------------

    [Fact]
    public async Task RNF8_logs_carry_the_operation_the_id_and_the_code_but_never_titles_addresses_bodies_tokens_or_secrets()
    {
        var data = GraphGatewayHarness.Data();
        const string leaked = "{\"error\":{\"code\":\"ErrorInvalidUser\",\"message\":\"The mailbox agenda@example.com leaked-detail-from-graph\"}}";
        _graph.Handler
            .Respond(HttpStatusCode.Forbidden, leaked, response => response.Headers.Add("request-id", "0196b7a0-2222-7000-8000-000000000002"))
            .Respond(HttpStatusCode.NotFound, leaked)
            .Respond(HttpStatusCode.NotFound, leaked)
            .Respond(HttpStatusCode.InternalServerError, leaked)
            .Respond(HttpStatusCode.TooManyRequests, leaked)
            .Run((_, _) => throw new HttpRequestException("failed for agenda@example.com " + StubTokenProvider.Token));

        await PublishAsync(data); // 403
        await PublishAsync(data, CreatedId); // 404 on the update and on the recreation
        await _graph.Gateway.CancelAsync(CreatedId, "motivo confidencial", Ct); // 500
        await PublishAsync(data); // 429: the gate closes
        _graph.Clock.Set(AgendaHarness.Now.AddMinutes(11));
        await PublishAsync(data); // network failure

        var logs = _graph.Logger.All;
        Assert.Contains(data.EventId.ToString("D"), logs, StringComparison.Ordinal);
        Assert.Contains(OutlookErrorCodes.Forbidden, logs, StringComparison.Ordinal);
        Assert.Contains("0196b7a0-2222-7000-8000-000000000002", logs, StringComparison.Ordinal);
        foreach (var secret in new[]
        {
            data.Subject, "Parches", data.BodyText, "Aplicar los parches", "ana.perez@example.com", "luis@example.com", "equipo@example.com",
            GraphGatewayHarness.Mailbox, "agenda@example.com", "leaked-detail-from-graph", "ErrorInvalidUser", "motivo confidencial",
            StubTokenProvider.Token, GraphGatewayHarness.ClientSecret, "Bearer", "Centro de datos",
        })
        {
            Assert.DoesNotContain(secret, logs, StringComparison.Ordinal);
        }

        Assert.All(_graph.Logger.Entries, entry => Assert.Null(entry.Exception));
    }

    [Fact]
    public void RNF8_the_settings_never_print_the_secret()
    {
        var settings = new GraphOutlookSettings("tenant", "client", "placeholder-not-a-secret-xyz", "agenda@example.com");

        Assert.DoesNotContain("placeholder-not-a-secret-xyz", settings.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("agenda@example.com", settings.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(StubTokenProvider.Token, GraphTokenResult.Success(StubTokenProvider.Token).ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "c", "s", "m@example.com", "tenantId")]
    [InlineData("t", " ", "s", "m@example.com", "clientId")]
    [InlineData("t", "c", "", "m@example.com", "clientSecret")]
    [InlineData("t", "c", "s", null, "mailbox")]
    public void Settings_require_every_value_and_the_message_names_the_parameter_not_the_value(string tenant, string client, string secret, string? mailbox, string parameter)
    {
        var failure = Assert.Throws<ArgumentException>(() => new GraphOutlookSettings(tenant, client, secret, mailbox!));

        Assert.Equal(parameter, failure.ParamName);
        Assert.DoesNotContain("m@example.com", failure.Message, StringComparison.Ordinal);
    }

    // ---- Mailbox in the path ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("agenda@example.com", "users/agenda%40example.com/calendar/events")]
    [InlineData("../../me@example.com", "users/..%2F..%2Fme%40example.com/calendar/events")]
    [InlineData("a b?c#d@example.com", "users/a%20b%3Fc%23d%40example.com/calendar/events")]
    public async Task A_mailbox_is_escaped_as_one_path_segment(string mailbox, string expectedPath)
    {
        var handler = new ScriptedGraphHandler().Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());
        var gateway = new GraphOutlookCalendarGateway(
            new HttpClient(handler), new StubTokenProvider(), new GraphOutlookSettings("t", "c", "s", mailbox),
            new GraphThrottleGate(TimeProvider.System), TimeProvider.System, new ListLogger<GraphOutlookCalendarGateway>());

        await gateway.PublishAsync(GraphGatewayHarness.Data(), null, Ct);

        var uri = Assert.Single(handler.Requests).Uri;
        Assert.Equal("graph.microsoft.com", uri.Host);
        Assert.Equal($"/v1.0/{expectedPath}", uri.AbsolutePath);
        Assert.Empty(uri.Query);
        Assert.Empty(uri.Fragment);
    }

    // ---- CA-14 to CA-16 through the use cases, with the real adapter ------------------------------------------------

    [Fact]
    public async Task CA14_scheduling_with_the_adapter_publishes_the_event_and_keeps_the_id_and_the_link()
    {
        var agenda = new AgendaHarness(outlookEnabled: true, gateway: _graph.Gateway);
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());
        var draft = await agenda.CreateDraftAsync(agenda.Input(participants: ["luis@example.com"]));

        var results = await agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);

        Assert.Equal(OutlookStatus.Published, results[0].Outlook.Status);
        Assert.Equal(CreatedId, draft.OutlookEventId);
        Assert.Equal("https://outlook.office365.com/owa/?itemid=abc&exvsurl=1", draft.OutlookWebLink);
        var json = Assert.Single(_graph.Handler.Requests).Json;
        Assert.Equal("[Mantenimiento y parches] Parches de SO", json.GetProperty("subject").GetString());
        Assert.Equal(draft.Id.ToString("D"), json.GetProperty("transactionId").GetString());
        var required = json.GetProperty("attendees").EnumerateArray().Where(a => a.GetProperty("type").GetString() == "required")
            .Select(a => a.GetProperty("emailAddress").GetProperty("address").GetString());
        Assert.Equal(["ana.perez@example.com", "luis@example.com"], required);
    }

    [Fact]
    public async Task CA16_a_batch_stops_at_the_first_429_and_the_rest_stay_pending_with_nothing_sent()
    {
        var agenda = new AgendaHarness(outlookEnabled: true, gateway: _graph.Gateway);
        var a = await agenda.CreateDraftAsync(agenda.Input(title: "A"));
        var b = await agenda.CreateDraftAsync(agenda.Input(title: "B"));
        var c = await agenda.CreateDraftAsync(agenda.Input(title: "C"));
        _graph.Handler
            .Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson(id: "AAMk-a="))
            .Respond(HttpStatusCode.TooManyRequests, null, response => response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30)));

        var results = await agenda.Schedule.HandleAsync(new ScheduleEventsCommand([a.Id, b.Id, c.Id]), Ct);

        Assert.Equal(2, _graph.Handler.Requests.Count);
        Assert.Equal(OutlookStatus.Published, results[0].Outlook.Status);
        Assert.Equal(OutlookStatus.Error, results[1].Outlook.Status);
        Assert.Equal(OutlookErrorCodes.Throttled, results[1].Outlook.ErrorCode);
        Assert.Equal(OutlookStatus.Pending, results[2].Outlook.Status);
        Assert.All(results, result => Assert.Equal(EventStatus.Scheduled, result.Status));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, OutlookErrorCodes.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, OutlookErrorCodes.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, OutlookErrorCodes.Unavailable)]
    public async Task CA16_a_failure_of_graph_leaves_the_event_scheduled_in_error_and_sync_again_publishes_it_without_duplicating(HttpStatusCode status, string code)
    {
        var agenda = new AgendaHarness(outlookEnabled: true, gateway: _graph.Gateway);
        var draft = await agenda.CreateDraftAsync();
        _graph.Handler.Respond(status).Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson());

        var scheduled = await agenda.Schedule.HandleAsync(new ScheduleEventsCommand([draft.Id]), Ct);
        var sync = await agenda.Sync.SyncOneAsync(draft.Id, Ct);

        Assert.Equal(EventStatus.Scheduled, scheduled[0].Status);
        Assert.Equal(OutlookStatus.Error, scheduled[0].Outlook.Status);
        Assert.Equal(code, scheduled[0].Outlook.ErrorCode);
        Assert.Equal(OutlookStatus.Published, sync.Outlook.Status);
        Assert.All(
            _graph.Handler.Requests,
            request => Assert.Equal(draft.Id.ToString("D"), request.Json.GetProperty("transactionId").GetString()));
    }

    [Fact]
    public async Task CA16_if_the_event_was_deleted_in_outlook_sync_again_creates_it_and_keeps_the_new_id()
    {
        var agenda = new AgendaHarness(outlookEnabled: true, gateway: _graph.Gateway);
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson(id: "AAMk-first="));
        var scheduled = await agenda.CreateScheduledAsync();
        _graph.Handler
            .Respond(HttpStatusCode.NotFound)
            .Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson(id: "AAMk-second="));

        var sync = await agenda.Sync.SyncOneAsync(scheduled.Id, Ct);

        Assert.Equal(OutlookStatus.Published, sync.Outlook.Status);
        Assert.Equal("AAMk-second=", scheduled.OutlookEventId);
        Assert.Equal([HttpMethod.Post, HttpMethod.Patch, HttpMethod.Post], _graph.Handler.Requests.Select(r => r.Method));
    }

    [Fact]
    public async Task CA15_cancelling_through_the_use_case_sends_the_reason_to_graph()
    {
        var agenda = new AgendaHarness(outlookEnabled: true, gateway: _graph.Gateway);
        _graph.Handler.Respond(HttpStatusCode.Created, GraphGatewayHarness.CreatedJson()).Respond(HttpStatusCode.Accepted);
        var scheduled = await agenda.CreateScheduledAsync();

        await agenda.Cancel.HandleAsync(new CancelEventCommand(scheduled.Id, "Se mueve al trimestre siguiente"), Ct);

        var cancel = _graph.Handler.Requests[^1];
        Assert.EndsWith("/cancel", cancel.Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("Se mueve al trimestre siguiente", cancel.Json.GetProperty("comment").GetString());
        Assert.Equal(EventStatus.Cancelled, scheduled.Status);
        Assert.Equal(OutlookStatus.Published, scheduled.OutlookStatus);
    }

    private sealed class SlowTokenProvider : IGraphTokenProvider
    {
        public async Task<GraphTokenResult> AcquireAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return GraphTokenResult.Success("never");
        }

        public void Invalidate()
        {
        }
    }

    private sealed class ThrowingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => throw new IOException("connection reset by peer");

        protected override Task<Stream> CreateContentReadStreamAsync() => throw new IOException("connection reset by peer");

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Infrastructure.Agenda.Graph;
using Itmas3.UnitTests.Application.Agenda;
using Microsoft.Extensions.Logging;

namespace Itmas3.UnitTests.Infrastructure.Agenda.Graph;

/// <summary>What the adapter sent: enough to check the contract with Graph. Nothing here talks to Microsoft.</summary>
internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? MediaType, string Body)
{
    public JsonElement Json => JsonDocument.Parse(Body).RootElement.Clone();
}

/// <summary>A <see cref="HttpMessageHandler"/> that answers from a script and records every request (SPEC-0006 §10.7, constitution §9.4).</summary>
internal sealed class ScriptedGraphHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _script = new();

    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>Answers the next request with <paramref name="status"/> and, optionally, a JSON body and extra headers.</summary>
    public ScriptedGraphHandler Respond(HttpStatusCode status, string? json = null, Action<HttpResponseMessage>? configure = null)
    {
        _script.Enqueue((_, _) =>
        {
            var response = new HttpResponseMessage(status);
            if (json is not null)
            {
                response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            configure?.Invoke(response);
            return Task.FromResult(response);
        });
        return this;
    }

    /// <summary>Answers the next request with whatever the function does (a delay, an exception…).</summary>
    public ScriptedGraphHandler Run(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action)
    {
        _script.Enqueue(action);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), request.Content?.Headers.ContentType?.MediaType, body));
        return _script.Count == 0
            ? throw new InvalidOperationException("The adapter sent a request nobody expected.")
            : await _script.Dequeue()(request, cancellationToken);
    }
}

internal sealed class StubTokenProvider : IGraphTokenProvider
{
    public const string Token = "test-access-token-not-real";

    public GraphTokenResult Result { get; set; } = GraphTokenResult.Success(Token);

    public int Acquisitions { get; private set; }

    public int Invalidations { get; private set; }

    public Task<GraphTokenResult> AcquireAsync(CancellationToken cancellationToken)
    {
        Acquisitions++;
        return Task.FromResult(Result);
    }

    public void Invalidate() => Invalidations++;
}

/// <summary>Keeps every message the adapter logs, so the tests can prove what never appears in them (RNF-8).</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public string All => string.Join('\n', Entries.Select(entry => entry.Message));

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception), exception));
}

/// <summary>The adapter wired to simulated parts, with the clock of the throttle gate under control.</summary>
internal sealed class GraphGatewayHarness
{
    public const string Mailbox = "agenda@example.com";
    public const string ClientSecret = "placeholder-not-a-secret";

    public GraphGatewayHarness(TimeSpan? requestTimeout = null)
    {
        Gate = new GraphThrottleGate(Clock);
        var settings = new GraphOutlookSettings("33333333-3333-3333-3333-333333333333", "44444444-4444-4444-4444-444444444444", ClientSecret, Mailbox)
        {
            RequestTimeout = requestTimeout ?? GraphOutlookSettings.DefaultRequestTimeout,
        };
        Gateway = new GraphOutlookCalendarGateway(new HttpClient(Handler), Tokens, settings, Gate, Clock, Logger);
    }

    public ScriptedGraphHandler Handler { get; } = new();

    public StubTokenProvider Tokens { get; } = new();

    public AdjustableClock Clock { get; } = new(AgendaHarness.Now);

    public GraphThrottleGate Gate { get; }

    public ListLogger<GraphOutlookCalendarGateway> Logger { get; } = new();

    public GraphOutlookCalendarGateway Gateway { get; }

    /// <summary>An event with markup, accents and every field the adapter sends.</summary>
    public static OutlookEventData Data(
        Guid? eventId = null,
        bool isAllDay = false,
        DateTime? start = null,
        DateTime? end = null,
        string? location = "Centro de datos",
        string[]? required = null,
        string[]? optional = null) =>
        new(
            eventId ?? Guid.Parse("0196b7a0-1111-7000-8000-000000000001"),
            "[Mantenimiento y parches] Parches <b>de</b> SO",
            "Aplicar los parches del mes <script>alert(1)</script>\n\nAlcance: Servidores Windows\n\nImpacto: Degradación del servicio",
            start ?? new DateTime(2026, 10, 24, 9, 0, 0),
            end ?? new DateTime(2026, 10, 24, 11, 0, 0),
            "America/Bogota",
            isAllDay,
            location,
            1440,
            "Mantenimiento y parches",
            required ?? ["ana.perez@example.com", "luis@example.com"],
            optional ?? ["equipo@example.com"]);

    public static string CreatedJson(string id = "AAMkAGI2-created=", string? webLink = "https://outlook.office365.com/owa/?itemid=abc&exvsurl=1") =>
        JsonSerializer.Serialize(new { id, webLink, subject = "never read", body = new { content = "never read" } });
}

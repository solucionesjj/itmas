using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Outlook;
using Microsoft.Extensions.Logging;

namespace Itmas3.Infrastructure.Agenda.Graph;

/// <summary>
/// The Outlook port over Microsoft Graph with an application token (SPEC-0006 §10.2, §10.7, ADR-0016): create (<c>POST</c> with the
/// id of the agenda event as <c>transactionId</c>), update (<c>PATCH</c>; a <c>404</c> creates the event again) and cancel
/// (<c>POST …/cancel</c> with the reason) in the default calendar of the shared mailbox.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Only <c>graph.microsoft.com</c>, over HTTPS, with fixed paths under <c>/users/{mailbox}/calendar/events</c>. Both the mailbox
/// (configuration) and the Outlook event id (kept from an earlier answer of Graph) are escaped or checked before they reach a path;
/// no URL comes from what people type. The client of the composition root does not follow redirects.</item>
/// <item>Every call has a time limit (<see cref="GraphOutlookSettings.RequestTimeout"/>, 10 s) that includes the token. No retries: a
/// <c>429</c> (or a <c>503</c> with <c>Retry-After</c>) closes the <see cref="GraphThrottleGate"/> and asks the caller to stop the batch.</item>
/// <item>Failures leave as a code of <see cref="OutlookErrorCodes"/>. The body of an answer of Graph is only read when the call
/// worked, and then only for <c>id</c> and <c>webLink</c>; it is never logged. Logs carry the operation, an id, the code and the
/// status: no titles, addresses or secrets (RNF-8).</item>
/// <item>The tags of ITMas are not sent (P-19); the type travels as the category.</item>
/// </list>
/// </remarks>
public sealed partial class GraphOutlookCalendarGateway : IOutlookCalendarGateway
{
    private const string GraphHost = "graph.microsoft.com";
    private const int MaxResponseBytes = 1_048_576;

    /// <summary>The column of the Outlook event id holds 300 characters.</summary>
    private const int MaxOutlookEventIdLength = 300;

    /// <summary>The column of the "Open in Outlook" link holds 1 000 characters.</summary>
    private const int MaxWebLinkLength = 1000;

    private static readonly Uri GraphRoot = new($"https://{GraphHost}/v1.0/");

    private readonly HttpClient _httpClient;
    private readonly IGraphTokenProvider _tokens;
    private readonly GraphOutlookSettings _settings;
    private readonly GraphThrottleGate _throttle;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GraphOutlookCalendarGateway> _logger;

    public GraphOutlookCalendarGateway(
        HttpClient httpClient,
        IGraphTokenProvider tokens,
        GraphOutlookSettings settings,
        GraphThrottleGate throttle,
        TimeProvider timeProvider,
        ILogger<GraphOutlookCalendarGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(throttle);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _tokens = tokens;
        _settings = settings;
        _throttle = throttle;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<OutlookGatewayResult> PublishAsync(OutlookEventData data, string? outlookEventId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        var reference = data.EventId.ToString("D");
        var transactionId = reference;

        if (!string.IsNullOrWhiteSpace(outlookEventId))
        {
            if (!IsSafeEventId(outlookEventId))
            {
                return OutlookGatewayResult.Failure(OutlookErrorCodes.Rejected);
            }

            var update = await SendAsync(HttpMethod.Patch, EventUri(outlookEventId), GraphEventPayload.ForEvent(data, null), "update", reference, readEvent: true, cancellationToken);
            if (update.Status != HttpStatusCode.NotFound)
            {
                return ToPublishResult(update, outlookEventId);
            }

            // The event is gone from the calendar (RN-14): create it again. Graph may still remember the transaction id of the one
            // that was removed, so the new one is derived from the id that no longer exists: the same on every retry of this
            // recreation (nothing is duplicated), different from the one of the event that was removed.
            LogRecreating(_logger, reference);
            transactionId = RecreationTransactionId(data.EventId, outlookEventId);
        }

        var create = await SendAsync(HttpMethod.Post, EventsUri(), GraphEventPayload.ForEvent(data, transactionId), "create", reference, readEvent: true, cancellationToken);
        return ToPublishResult(create, null);
    }

    public async Task<OutlookGatewayResult> CancelAsync(string outlookEventId, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(outlookEventId) || !IsSafeEventId(outlookEventId))
        {
            return OutlookGatewayResult.Failure(OutlookErrorCodes.Rejected);
        }

        var cancel = await SendAsync(
            HttpMethod.Post, EventUri(outlookEventId, "cancel"), GraphEventPayload.ForCancellation(reason ?? string.Empty), "cancel", outlookEventId, readEvent: false, cancellationToken);

        // An event that no longer exists is as cancelled as it can be (port contract).
        return cancel.ErrorCode is null || cancel.Status == HttpStatusCode.NotFound
            ? OutlookGatewayResult.Success(outlookEventId, null)
            : OutlookGatewayResult.Failure(cancel.ErrorCode, cancel.Throttled);
    }

    /// <summary>Only characters that Graph uses in event ids (letters, digits, <c>-</c>, <c>_</c>, <c>=</c>, <c>+</c>): nothing that can change a path.</summary>
    internal static bool IsSafeEventId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= MaxOutlookEventIdLength && id.All(IsSafeIdCharacter);

    private static bool IsSafeIdCharacter(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '=' or '+';

    private static string RecreationTransactionId(Guid eventId, string removedOutlookEventId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{eventId:D}:{removedOutlookEventId}"));
        return new Guid(hash.AsSpan(0, 16)).ToString("D");
    }

    private Uri EventsUri() => new(GraphRoot, $"users/{Uri.EscapeDataString(_settings.Mailbox)}/calendar/events");

    private Uri EventUri(string outlookEventId, string? action = null) =>
        new(GraphRoot, $"users/{Uri.EscapeDataString(_settings.Mailbox)}/calendar/events/{Uri.EscapeDataString(outlookEventId)}{(action is null ? string.Empty : "/" + action)}");

    private static OutlookGatewayResult ToPublishResult(GraphCall call, string? existingOutlookEventId)
    {
        if (call.ErrorCode is not null)
        {
            return OutlookGatewayResult.Failure(call.ErrorCode, call.Throttled);
        }

        var id = call.EventId ?? existingOutlookEventId;
        return IsSafeEventId(id)
            ? OutlookGatewayResult.Success(id!, call.WebLink)
            : OutlookGatewayResult.Failure(OutlookErrorCodes.Rejected);
    }

    /// <summary>One call to Graph: the gate, the token, the request, the status and, when it worked, the id and the link of the event.</summary>
    private async Task<GraphCall> SendAsync(
        HttpMethod method, Uri uri, byte[] body, string operation, string reference, bool readEvent, CancellationToken cancellationToken)
    {
        if (_throttle.IsClosed(out var remaining))
        {
            LogWaiting(_logger, operation, reference, (int)Math.Ceiling(remaining.TotalSeconds));
            return GraphCall.Failed(null, OutlookErrorCodes.Throttled, throttled: true);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_settings.RequestTimeout);
        try
        {
            var token = await _tokens.AcquireAsync(timeout.Token);
            if (token.AccessToken is null)
            {
                var code = OutlookErrorCodes.Normalize(token.ErrorCode);
                var tokenThrottled = code == OutlookErrorCodes.Throttled;
                if (tokenThrottled)
                {
                    _throttle.Close(token.RetryAfter);
                }

                LogFailed(_logger, operation, reference, code, 0, null);
                return GraphCall.Failed(null, code, tokenThrottled);
            }

            using var request = new HttpRequestMessage(method, uri) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var status = response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                return Rejection(response, status, operation, reference);
            }

            if (!readEvent)
            {
                LogSucceeded(_logger, operation, reference, (int)status);
                return new GraphCall(status, null, false, null, null);
            }

            var (eventId, webLink) = await ReadEventAsync(response, timeout.Token);
            LogSucceeded(_logger, operation, reference, (int)status);
            return new GraphCall(status, null, false, eventId, webLink);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogFailed(_logger, operation, reference, OutlookErrorCodes.Timeout, 0, null);
            return GraphCall.Failed(null, OutlookErrorCodes.Timeout);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            // Network failure: the message could name hosts or addresses, so only the closed code goes out.
            LogFailed(_logger, operation, reference, OutlookErrorCodes.Unavailable, 0, null);
            return GraphCall.Failed(null, OutlookErrorCodes.Unavailable);
        }
    }

    /// <summary>Maps a status that is not a success to the closed set of codes (SPEC-0006 §12.1) without looking at the body.</summary>
    private GraphCall Rejection(HttpResponseMessage response, HttpStatusCode status, string operation, string reference)
    {
        var code = status switch
        {
            HttpStatusCode.Unauthorized => OutlookErrorCodes.Unauthorized,
            HttpStatusCode.Forbidden => OutlookErrorCodes.Forbidden,
            HttpStatusCode.NotFound => OutlookErrorCodes.NotFound,
            HttpStatusCode.RequestTimeout => OutlookErrorCodes.Timeout,
            HttpStatusCode.TooManyRequests => OutlookErrorCodes.Throttled,
            >= HttpStatusCode.InternalServerError => OutlookErrorCodes.Unavailable,
            _ => OutlookErrorCodes.Rejected,
        };

        if (status == HttpStatusCode.Unauthorized)
        {
            _tokens.Invalidate();
        }

        var retryAfter = RetryAfter(response.Headers.RetryAfter);
        var throttled = status == HttpStatusCode.TooManyRequests || (status == HttpStatusCode.ServiceUnavailable && retryAfter is not null);
        if (throttled)
        {
            _throttle.Close(retryAfter);
        }

        LogFailed(_logger, operation, reference, code, (int)status, RequestId(response));
        return GraphCall.Failed(status, code, throttled);
    }

    private TimeSpan? RetryAfter(RetryConditionHeaderValue? header) =>
        header?.Delta ?? (header?.Date is { } date ? date - _timeProvider.GetUtcNow() : null);

    /// <summary>The id Graph gave to the event and a link to open it, read from a bounded body. Nothing else is looked at, kept or logged.</summary>
    private static async Task<(string? EventId, string? WebLink)> ReadEventAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaxResponseBytes)
        {
            return (null, null);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxResponseBytes)
            {
                return (null, null);
            }

            buffer.Write(chunk, 0, read);
        }

        try
        {
            using var document = JsonDocument.Parse(buffer.GetBuffer().AsMemory(0, (int)buffer.Length));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            var root = document.RootElement;
            var id = root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : null;
            var link = root.TryGetProperty("webLink", out var linkElement) && linkElement.ValueKind == JsonValueKind.String ? linkElement.GetString() : null;
            return (id, SafeWebLink(link));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    /// <summary>The link is shown to people as "Open in Outlook": only an absolute HTTPS address without credentials that fits its column is kept.</summary>
    private static string? SafeWebLink(string? link) =>
        link is { Length: > 0 and <= MaxWebLinkLength }
        && Uri.TryCreate(link, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo)
            ? link
            : null;

    /// <summary>The id of the request that Graph assigns, to help Microsoft support: only when it is a GUID, so it cannot carry anything else.</summary>
    private static string? RequestId(HttpResponseMessage response) =>
        response.Headers.TryGetValues("request-id", out var values) && Guid.TryParse(values.FirstOrDefault(), out var id)
            ? id.ToString("D")
            : null;

    private readonly record struct GraphCall(HttpStatusCode? Status, string? ErrorCode, bool Throttled, string? EventId, string? WebLink)
    {
        public static GraphCall Failed(HttpStatusCode? status, string errorCode, bool throttled = false) => new(status, errorCode, throttled, null, null);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outlook {Operation} of {Reference} failed: {ErrorCode} (HTTP {StatusCode}, Graph request {RequestId})")]
    private static partial void LogFailed(ILogger logger, string operation, string reference, string errorCode, int statusCode, string? requestId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Outlook {Operation} of {Reference} done (HTTP {StatusCode})")]
    private static partial void LogSucceeded(ILogger logger, string operation, string reference, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outlook {Operation} of {Reference} not sent: Graph asked to wait {Seconds} more seconds")]
    private static partial void LogWaiting(ILogger logger, string operation, string reference, int seconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "The Outlook event of {Reference} no longer exists: it is created again")]
    private static partial void LogRecreating(ILogger logger, string reference);
}

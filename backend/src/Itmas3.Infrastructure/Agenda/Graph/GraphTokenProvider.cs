using System.Net;
using Itmas3.Application.Agenda;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Itmas3.Infrastructure.Agenda.Graph;

/// <summary>
/// Outcome of asking for the application token of Graph. Either <see cref="AccessToken"/> or a normalized
/// <see cref="ErrorCode"/> (<see cref="OutlookErrorCodes"/>) is set. The token is a secret: it is not part of
/// <see cref="ToString"/>.
/// </summary>
public sealed class GraphTokenResult
{
    private GraphTokenResult(string? accessToken, string? errorCode, TimeSpan? retryAfter)
    {
        AccessToken = accessToken;
        ErrorCode = errorCode;
        RetryAfter = retryAfter;
    }

    /// <summary>SECRET. Bearer token for the calls to Graph.</summary>
    public string? AccessToken { get; }

    public string? ErrorCode { get; }

    /// <summary>How long the identity platform asked to wait, when it throttled the request.</summary>
    public TimeSpan? RetryAfter { get; }

    public static GraphTokenResult Success(string accessToken) => new(accessToken, null, null);

    public static GraphTokenResult Failure(string errorCode, TimeSpan? retryAfter = null) => new(null, errorCode, retryAfter);

    public override string ToString() => nameof(GraphTokenResult);
}

/// <summary>The application token of Graph (ADR-0016 §2, §4). Never reaches the browser.</summary>
public interface IGraphTokenProvider
{
    /// <summary>Returns a valid token (from the in-memory cache when possible) or a normalized failure. Never throws for an expected failure.</summary>
    Task<GraphTokenResult> AcquireAsync(CancellationToken cancellationToken);

    /// <summary>Graph rejected the last token (401): the next call asks for a new one instead of using the cache.</summary>
    void Invalidate();
}

/// <summary>
/// MSAL (<c>Microsoft.Identity.Client</c>) client-credentials flow with the application of Entra ID of the agenda (ADR-0016). The
/// authority is fixed to <c>login.microsoftonline.com</c>; only the tenant comes from the configuration. MSAL keeps the token in
/// memory and reuses it until it is about to expire. Nothing is logged but the closed codes of MSAL and the HTTP status: never
/// the message of an exception, which can carry identifiers of the tenant (RNF-8).
/// </summary>
public sealed partial class MsalGraphTokenProvider : IGraphTokenProvider
{
    /// <summary>Scope of an application token for Graph: the permissions granted to the application, which are those of RBAC (ADR-0016 §2).</summary>
    internal const string GraphScope = "https://graph.microsoft.com/.default";

    private readonly GraphOutlookSettings _settings;
    private readonly IMsalHttpClientFactory? _httpClientFactory;
    private readonly ILogger<MsalGraphTokenProvider> _logger;
    private readonly Lock _lock = new();
    private IConfidentialClientApplication? _application;
    private volatile bool _forceRefresh;

    public MsalGraphTokenProvider(GraphOutlookSettings settings, ILogger<MsalGraphTokenProvider> logger)
        : this(settings, logger, null)
    {
    }

    /// <summary>For the tests: lets them answer the calls of MSAL instead of the identity platform.</summary>
    internal MsalGraphTokenProvider(GraphOutlookSettings settings, ILogger<MsalGraphTokenProvider> logger, IMsalHttpClientFactory? httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);
        _settings = settings;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<GraphTokenResult> AcquireAsync(CancellationToken cancellationToken)
    {
        var application = GetApplication();
        if (application is null)
        {
            return GraphTokenResult.Failure(OutlookErrorCodes.NotConfigured);
        }

        try
        {
            var request = application.AcquireTokenForClient([GraphScope]);
            if (_forceRefresh)
            {
                request = request.WithForceRefresh(true);
            }

            var result = await request.ExecuteAsync(cancellationToken);
            _forceRefresh = false;
            return string.IsNullOrWhiteSpace(result.AccessToken)
                ? GraphTokenResult.Failure(OutlookErrorCodes.Unauthorized)
                : GraphTokenResult.Success(result.AccessToken);
        }
        catch (MsalServiceException exception)
        {
            var failure = Map(exception);
            LogTokenFailed(_logger, failure.ErrorCode!, exception.ErrorCode, exception.StatusCode);
            return failure;
        }
        catch (MsalClientException exception)
        {
            LogTokenFailed(_logger, OutlookErrorCodes.Unavailable, exception.ErrorCode, 0);
            return GraphTokenResult.Failure(OutlookErrorCodes.Unavailable);
        }
    }

    public void Invalidate() => _forceRefresh = true;

    /// <summary>A 429 asks to wait, a 5xx or a network failure is the platform's problem, a timeout is a timeout and any other answer means the credential was refused.</summary>
    internal static GraphTokenResult Map(MsalServiceException exception)
    {
        if (exception.StatusCode == (int)HttpStatusCode.TooManyRequests)
        {
            return GraphTokenResult.Failure(OutlookErrorCodes.Throttled, exception.Headers?.RetryAfter?.Delta);
        }

        if (string.Equals(exception.ErrorCode, MsalError.RequestTimeout, StringComparison.Ordinal))
        {
            return GraphTokenResult.Failure(OutlookErrorCodes.Timeout);
        }

        return exception.StatusCode >= (int)HttpStatusCode.InternalServerError
            || string.Equals(exception.ErrorCode, MsalError.ServiceNotAvailable, StringComparison.Ordinal)
                ? GraphTokenResult.Failure(OutlookErrorCodes.Unavailable)
                : GraphTokenResult.Failure(OutlookErrorCodes.Unauthorized);
    }

    /// <summary>Builds the MSAL application once. A configuration MSAL does not accept is reported as <c>not_configured</c>, not as a crash.</summary>
    private IConfidentialClientApplication? GetApplication()
    {
        lock (_lock)
        {
            if (_application is not null)
            {
                return _application;
            }

            try
            {
                var builder = ConfidentialClientApplicationBuilder.Create(_settings.ClientId)
                    .WithClientSecret(_settings.ClientSecret)
                    .WithAuthority(AzureCloudInstance.AzurePublic, _settings.TenantId)
                    // The authority is the fixed public host, not something to validate: this saves a call to the discovery endpoint.
                    .WithInstanceDiscovery(false);
                if (_httpClientFactory is not null)
                {
                    builder = builder.WithHttpClientFactory(_httpClientFactory);
                }

                _application = builder.Build();
                return _application;
            }
            catch (Exception exception) when (exception is ArgumentException or MsalClientException)
            {
                LogApplicationRejected(_logger, exception.GetType().Name);
                return null;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not get the Graph token: {ErrorCode} (MSAL {MsalCode}, HTTP {StatusCode})")]
    private static partial void LogTokenFailed(ILogger logger, string errorCode, string msalCode, int statusCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "The Graph application settings were rejected by MSAL ({ExceptionType})")]
    private static partial void LogApplicationRejected(ILogger logger, string exceptionType);
}

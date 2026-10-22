using System.Net;
using System.Text;
using Itmas3.Application.Agenda;
using Itmas3.Infrastructure.Agenda.Graph;
using Microsoft.Identity.Client;

namespace Itmas3.UnitTests.Infrastructure.Agenda.Graph;

/// <summary>
/// The application token of Graph (ADR-0016 §2, §4) with MSAL answering from a simulated handler: the authority is fixed to
/// <c>login.microsoftonline.com</c>, the token is cached, and failures leave as closed codes without detail (RNF-8).
/// </summary>
public sealed class MsalGraphTokenProviderTests
{
    private const string ClientSecret = "placeholder-not-a-secret-msal";

    // MSAL keeps throttling state for the whole process, keyed by tenant, client and scope: each test uses its own so that a 429
    // simulated by one never answers another.
    private readonly string _tenantId = Guid.NewGuid().ToString("D");
    private readonly string _clientId = Guid.NewGuid().ToString("D");
    private readonly ScriptedGraphHandler _identity = new();
    private readonly ListLogger<MsalGraphTokenProvider> _logger = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private MsalGraphTokenProvider Provider(string? tenant = null) => new(
        new GraphOutlookSettings(tenant ?? _tenantId, _clientId, ClientSecret, "agenda@example.com"),
        _logger,
        new SimulatedMsalHttp(_identity));

    private static string TokenJson(string token = "fake-app-token-1") =>
        $"{{\"token_type\":\"Bearer\",\"expires_in\":3600,\"ext_expires_in\":3600,\"access_token\":\"{token}\"}}";

    [Fact]
    public async Task The_token_is_requested_from_the_fixed_authority_with_the_graph_scope_and_the_client_credentials()
    {
        _identity.Respond(HttpStatusCode.OK, TokenJson());

        var result = await Provider().AcquireAsync(Ct);

        Assert.Equal("fake-app-token-1", result.AccessToken);
        Assert.Null(result.ErrorCode);
        var request = Assert.Single(_identity.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https", request.Uri.Scheme);
        Assert.Equal("login.microsoftonline.com", request.Uri.Host);
        Assert.Equal($"/{_tenantId}/oauth2/v2.0/token", request.Uri.AbsolutePath);
        var form = System.Web.HttpUtility.ParseQueryString(request.Body);
        Assert.Equal("client_credentials", form["grant_type"]);
        Assert.Equal("https://graph.microsoft.com/.default", form["scope"]);
        Assert.Equal(_clientId, form["client_id"]);
    }

    [Fact]
    public async Task The_token_is_kept_in_memory_and_reused_until_a_401_asks_for_a_new_one()
    {
        _identity.Respond(HttpStatusCode.OK, TokenJson("fake-app-token-1")).Respond(HttpStatusCode.OK, TokenJson("fake-app-token-2"));
        var provider = Provider();

        var first = await provider.AcquireAsync(Ct);
        var second = await provider.AcquireAsync(Ct);
        provider.Invalidate();
        var third = await provider.AcquireAsync(Ct);
        var fourth = await provider.AcquireAsync(Ct);

        Assert.Equal("fake-app-token-1", first.AccessToken);
        Assert.Equal("fake-app-token-1", second.AccessToken);
        Assert.Equal("fake-app-token-2", third.AccessToken);
        Assert.Equal("fake-app-token-2", fourth.AccessToken);
        Assert.Equal(2, _identity.Requests.Count);
    }

    [Fact]
    public async Task A_refused_credential_is_graph_unauthorized_and_neither_the_secret_nor_the_detail_is_logged()
    {
        _identity.Respond(
            HttpStatusCode.Unauthorized,
            $"{{\"error\":\"invalid_client\",\"error_description\":\"AADSTS7000215: Invalid client secret {ClientSecret} for agenda@example.com\",\"error_codes\":[7000215]}}");

        var result = await Provider().AcquireAsync(Ct);

        Assert.Null(result.AccessToken);
        Assert.Equal(OutlookErrorCodes.Unauthorized, result.ErrorCode);
        Assert.Contains(OutlookErrorCodes.Unauthorized, _logger.All, StringComparison.Ordinal);
        foreach (var leaked in new[] { ClientSecret, "agenda@example.com", "AADSTS7000215", "Invalid client secret", _tenantId })
        {
            Assert.DoesNotContain(leaked, _logger.All, StringComparison.Ordinal);
        }

        Assert.All(_logger.Entries, entry => Assert.Null(entry.Exception));
    }

    [Fact]
    public async Task A_throttled_token_request_reports_graph_throttled_with_the_wait_asked_by_the_platform()
    {
        _identity.Respond(
            HttpStatusCode.TooManyRequests,
            "{\"error\":\"temporarily_unavailable\",\"error_description\":\"throttled\"}",
            response => response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30)));

        var result = await Provider().AcquireAsync(Ct);

        Assert.Equal(OutlookErrorCodes.Throttled, result.ErrorCode);
        Assert.Equal(TimeSpan.FromSeconds(30), result.RetryAfter);
    }

    [Theory]
    [InlineData(429, "temporarily_unavailable", OutlookErrorCodes.Throttled)]
    [InlineData(500, "server_error", OutlookErrorCodes.Unavailable)]
    [InlineData(503, "service_not_available", OutlookErrorCodes.Unavailable)]
    [InlineData(0, "service_not_available", OutlookErrorCodes.Unavailable)]
    [InlineData(0, "request_timeout", OutlookErrorCodes.Timeout)]
    [InlineData(400, "invalid_request", OutlookErrorCodes.Unauthorized)]
    [InlineData(400, "unauthorized_client", OutlookErrorCodes.Unauthorized)]
    [InlineData(401, "invalid_client", OutlookErrorCodes.Unauthorized)]
    [InlineData(404, "tenant_not_found", OutlookErrorCodes.Unauthorized)]
    public void Failures_of_the_identity_platform_map_to_the_closed_codes(int status, string msalCode, string expected)
    {
        var failure = new MsalServiceException(msalCode, "detail with agenda@example.com that must not leave", status, null, null);

        var result = MsalGraphTokenProvider.Map(failure);

        Assert.Equal(expected, result.ErrorCode);
        Assert.Null(result.AccessToken);
    }

    [Fact]
    public async Task A_tenant_that_msal_does_not_accept_is_not_configured_and_never_calls_the_network()
    {
        var result = await Provider(tenant: "has spaces/and/slashes").AcquireAsync(Ct);

        Assert.Equal(OutlookErrorCodes.NotConfigured, result.ErrorCode);
        Assert.Empty(_identity.Requests);
        Assert.DoesNotContain("has spaces", _logger.All, StringComparison.Ordinal);
    }

    /// <summary>MSAL's own HTTP client, replaced by the simulated handler so no test reaches the identity platform.</summary>
    private sealed class SimulatedMsalHttp(HttpMessageHandler handler) : IMsalHttpClientFactory
    {
        public HttpClient GetHttpClient() => new(handler, disposeHandler: false);
    }
}

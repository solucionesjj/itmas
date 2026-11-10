using System.Net;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Infrastructure.Agenda.Smtp;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MimeKit;
using Npgsql;

namespace Itmas3.IntegrationTests.Api.Agenda;

/// <summary>
/// A test SMTP server (Mailpit, pinned by digest) with STARTTLS required and a certificate signed by a CA made for each run. It accepts any
/// authentication and keeps the messages, which its HTTP API returns. Nothing leaves the machine (constitution §9.4).
/// </summary>
public class SmtpServerFixture : IAsyncLifetime
{
    /// <summary>Mailpit v1.32.0 (2026-10-22), pinned by digest (SPEC-0008 §10, ADR-0023 decision 9).</summary>
    public const string Image = "axllent/mailpit:v1.32.0@sha256:0790692be935ea4420d085694f7985b1f692ca6a8771ab0e0bd8ba75ac180498";

    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;

    private readonly IContainer _container;

    public SmtpServerFixture()
        : this(tls: true)
    {
    }

    /// <summary>With <paramref name="tls"/> false the server offers no STARTTLS at all, to prove the adapter never sends in clear.</summary>
    protected SmtpServerFixture(bool tls)
    {
        var now = DateTimeOffset.UtcNow;
        using var caKey = RSA.Create(2048);
        var caRequest = new CertificateRequest("CN=ITMas test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        Authority = caRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));

        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=localhost", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        names.AddIpAddress(IPAddress.IPv6Loopback);
        leafRequest.CertificateExtensions.Add(names.Build());
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        leafRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var leaf = leafRequest.Create(Authority, now.AddDays(-1), now.AddDays(1), RandomNumberGenerator.GetBytes(16));

        var builder = new ContainerBuilder(Image)
            .WithPortBinding(SmtpPort, true)
            .WithPortBinding(HttpPort, true)
            .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "true")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(HttpPort).ForPath("/readyz")));
        if (tls)
        {
            builder = builder
                .WithResourceMapping(System.Text.Encoding.ASCII.GetBytes(leaf.ExportCertificatePem()), "/certs/cert.pem")
                .WithResourceMapping(System.Text.Encoding.ASCII.GetBytes(leafKey.ExportPkcs8PrivateKeyPem()), "/certs/key.pem")
                .WithEnvironment("MP_SMTP_TLS_CERT", "/certs/cert.pem")
                .WithEnvironment("MP_SMTP_TLS_KEY", "/certs/key.pem")
                .WithEnvironment("MP_SMTP_REQUIRE_STARTTLS", "true");
        }
        else
        {
            builder = builder.WithEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "true");
        }

        _container = builder.Build();
    }

    /// <summary>The CA of this run: the only thing the test trust accepts.</summary>
    public X509Certificate2 Authority { get; }

    public string Host => _container.Hostname;

    public int Port => _container.GetMappedPublicPort(SmtpPort);

    public Uri Api => new($"http://{_container.Hostname}:{_container.GetMappedPublicPort(HttpPort)}/");

    /// <summary>
    /// Trusts only certificates that chain to <see cref="Authority"/> and match the host name (revocation is not checked: the test CA
    /// publishes no list). It exists only here: the composition of production validates strictly (ADR-0023 decision 1).
    /// </summary>
    public bool Trust(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (certificate is null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
        {
            return false;
        }

        using var presented = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        using var custom = new X509Chain();
        custom.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        custom.ChainPolicy.CustomTrustStore.Add(Authority);
        custom.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return custom.Build(presented);
    }

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    /// <summary>How many messages the server received.</summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        using var http = new HttpClient { BaseAddress = Api };
        var list = await http.GetFromJsonAsync<JsonElement>("api/v1/messages?limit=1", cancellationToken);
        return list.GetProperty("total").GetInt32();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        Authority.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>The same test server without STARTTLS.</summary>
public sealed class PlainSmtpServerFixture : SmtpServerFixture
{
    public PlainSmtpServerFixture()
        : base(tls: false)
    {
    }
}

/// <summary>Keeps every log line and exception the API writes, to prove nothing of the SMTP server or the addresses reaches them.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();

    public IReadOnlyCollection<string> Lines => _lines;

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(CapturingLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner._lines.Enqueue($"{category} {formatter(state, exception)} {exception}");
    }
}

/// <summary>
/// SPEC-0008 CA-8 ("integración" and "persistencia") and CA-3b: the real API, PostgreSQL and the real SMTP adapter against the test
/// server. What leaves ITMas (the iCalendar methods, sequences and recipients), what is stored (who may hold the meeting) and what never
/// comes out (the list in the API, the export and the audit), plus the <c>CHECK</c> of the column.
/// </summary>
public sealed class AgendaSmtpIntegrationTests(PostgreSqlFixture database, SmtpServerFixture smtp, PlainSmtpServerFixture plain)
    : AgendaTestBase(database), IClassFixture<SmtpServerFixture>, IClassFixture<PlainSmtpServerFixture>
{
    private const string Sender = "agenda@example.com";

    private sealed record Sent(string Method, int Sequence, string[] To, string[] Cc, string Calendar);

    private async Task<(WebApplicationFactory<Program> App, HttpClient Client, TestUser User)> StartAsync(
        SmtpServerFixture? server = null,
        ISmtpTransportFactory? factory = null,
        ILoggerProvider? logs = null,
        TimeProvider? clock = null)
    {
        var target = server ?? smtp;
        var app = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Agenda:Outlook:Enabled", "true");
            builder.UseSetting("Agenda:Outlook:Transport", "smtp");
            builder.UseSetting("Agenda:Smtp:Username", Sender);
            builder.UseSetting("Agenda:Smtp:Password", "placeholder-not-a-secret");
            builder.UseSetting("Agenda:PublicUrl", "https://itmas.example.com");
            if (logs is not null)
            {
                // Everything the API writes, not only warnings, so the absence below proves something.
                builder.UseSetting("Logging:LogLevel:Default", "Trace");
                builder.UseSetting("Logging:LogLevel:Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware", "Trace");
            }

            builder.ConfigureTestServices(services =>
            {
                // Only the host, the port and the trust change: everything else is the composition of production.
                services.RemoveAll<SmtpOutlookSettings>();
                services.AddSingleton(new SmtpOutlookSettings(Sender, "placeholder-not-a-secret", target.Host, target.Port));
                services.RemoveAll<ISmtpTransportFactory>();
                services.AddSingleton(factory ?? new MailKitSmtpTransportFactory(target.Trust));
                if (logs is not null)
                {
                    services.AddSingleton(logs);
                }

                if (clock is not null)
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton(clock);
                }
            });
        });
        var user = await Factory.CreateUserAsync(false, AllPermissions);
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, user.Id.ToString());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.StampHeader, user.SecurityStamp);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.NameHeader, user.DisplayName);
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        return (app, client, user);
    }

    /// <summary>The messages the test server received for one event, in the order of their SEQUENCE (unique per message).</summary>
    private async Task<List<Sent>> SentForAsync(Guid eventId)
    {
        using var http = new HttpClient { BaseAddress = smtp.Api };
        var list = await http.GetFromJsonAsync<JsonElement>("api/v1/messages?limit=500", Ct);
        var sent = new List<Sent>();
        foreach (var item in list.GetProperty("messages").EnumerateArray().Reverse())
        {
            await using var raw = await http.GetStreamAsync($"api/v1/message/{item.GetProperty("ID").GetString()}/raw", Ct);
            var message = await MimeMessage.LoadAsync(raw, Ct);
            var calendar = message.BodyParts.OfType<TextPart>().Single(part => part.ContentType.MediaSubtype == "calendar");
            // Unfolded, with LF line ends whatever the server stored.
            var text = calendar.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n ", string.Empty, StringComparison.Ordinal);
            if (!text.Contains($"UID:{InvitationWriter.UidOf(eventId)}", StringComparison.Ordinal))
            {
                continue;
            }

            var sequence = text.Split('\n').Single(line => line.StartsWith("SEQUENCE:", StringComparison.Ordinal))["SEQUENCE:".Length..];
            sent.Add(new Sent(
                calendar.ContentType.Parameters["method"] ?? string.Empty,
                int.Parse(sequence, System.Globalization.CultureInfo.InvariantCulture),
                [.. message.To.Mailboxes.Select(mailbox => mailbox.Address)],
                [.. message.Cc.Mailboxes.Select(mailbox => mailbox.Address)],
                text));
        }

        // The cancellation to whoever was removed and the update leave milliseconds apart: order by SEQUENCE, not by arrival.
        return [.. sent.OrderBy(message => message.Sequence)];
    }

    private async Task<IReadOnlyList<string>?> StoredRecipientsAsync(Guid eventId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.AgendaEvents.AsNoTracking().SingleAsync(agendaEvent => agendaEvent.Id == eventId, Ct)).OutlookSentRecipients;
    }

    [Fact]
    public async Task CA3b_scheduling_removing_a_participant_and_cancelling_send_the_right_invitations_and_keep_the_list()
    {
        var (app, client, user) = await StartAsync();
        await using var _ = app;
        using var __ = client;
        var id = (await CreateEventAsync(client, EventBody(user.Id, title: Unique("SMTP"), participants: ["luis@example.com"])))
            .GetProperty("id").GetGuid();

        var scheduled = await ScheduleAsync(client, id);
        Assert.Equal("published", scheduled.Of(id).GetProperty("outlook").GetProperty("status").GetString());
        var stored = await StoredRecipientsAsync(id);
        Assert.NotNull(stored);
        Assert.Contains("luis@example.com", stored);
        Assert.Equal(2, stored.Count);

        // Remove the participant: one CANCEL only to him, then the REQUEST to the rest, with the sequences of ADR-0023.
        var detail = await GetEventAsync(client, id);
        var body = EventBody(user.Id, title: detail.GetProperty("title").GetString(), start: detail.GetProperty("start").GetString()![..16], participants: []);
        using var update = await client.SendAsync(Put(EventUrl(id), body, detail.GetProperty("revision").GetInt32()), Ct);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.DoesNotContain("luis@example.com", (await StoredRecipientsAsync(id))!);

        using var cancel = await client.PostAsJsonAsync(EventUrl(id, "/cancel"), new { reason = "Se movió a enero" }, Ct);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Null(await StoredRecipientsAsync(id));

        var sent = await SentForAsync(id);
        Assert.Equal(["REQUEST", "CANCEL", "REQUEST", "CANCEL"], sent.Select(message => message.Method));
        Assert.Contains("luis@example.com", sent[0].To);
        Assert.Equal(["luis@example.com"], sent[1].To);
        Assert.DoesNotContain(detail.GetProperty("title").GetString()!, sent[1].Calendar, StringComparison.Ordinal);
        Assert.Contains($"SUMMARY:{InvitationWriter.WithdrawalSubject}", sent[1].Calendar, StringComparison.Ordinal);
        Assert.DoesNotContain("luis@example.com", sent[2].To);
        Assert.True(sent[2].Sequence == sent[1].Sequence + 1 && sent[1].Sequence > sent[0].Sequence);
        Assert.True(sent[3].Sequence > sent[2].Sequence);
        Assert.Contains("COMMENT:Se movió a enero", sent[3].Calendar, StringComparison.Ordinal);
        Assert.DoesNotContain("luis@example.com", sent[3].To);
        Assert.Contains("ORGANIZER:mailto:agenda@example.com", sent[0].Calendar, StringComparison.Ordinal);
        Assert.Equal(2, sent[0].To.Length); // the responsible and the participant
    }

    [Fact]
    public async Task The_list_never_comes_out_through_the_api_the_export_or_the_audit_of_outlook()
    {
        var (app, client, user) = await StartAsync();
        await using var _ = app;
        using var __ = client;
        var title = Unique("Lista");
        var id = (await CreateEventAsync(client, EventBody(user.Id, title: title, participants: ["marta@example.com"])))
            .GetProperty("id").GetGuid();
        await ScheduleAsync(client, id);
        Assert.NotNull(await StoredRecipientsAsync(id));

        var detail = (await GetEventAsync(client, id)).GetRawText();
        Assert.DoesNotContain("sentRecipients", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InvitationWriter.UidOf(id), detail, StringComparison.Ordinal);

        var export = await client.GetStringAsync(Url($"/api/v1/agenda-events/export.ics?from={DateTime.UtcNow:yyyy-MM-dd}&to={DateTime.UtcNow.AddDays(30):yyyy-MM-dd}"), Ct);
        Assert.Contains(title, export, StringComparison.Ordinal);
        Assert.DoesNotContain("marta@example.com", export, StringComparison.Ordinal);

        var outlookAudit = await AuditAsync("agenda.event.outlook");
        Assert.Contains(outlookAudit, row => row.ResourceId == id.ToString());
        Assert.DoesNotContain(outlookAudit, row => row.ResourceId == id.ToString() && row.After!.Contains('@'));

        // No entry of the activity, its own snapshots included, carries the list.
        var audit = await AuditAsync("agenda.event");
        Assert.DoesNotContain(audit, row => row.ResourceId == id.ToString()
            && $"{row.Before}{row.After}".Contains("ecipients\":[", StringComparison.OrdinalIgnoreCase)
            && $"{row.Before}{row.After}".Contains("sent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RNF1_without_starttls_nothing_is_sent_and_nothing_of_the_server_is_logged()
    {
        var logs = new CapturingLoggerProvider();
        var (app, client, user) = await StartAsync(server: plain, logs: logs);
        await using var _ = app;
        using var __ = client;
        var id = (await CreateEventAsync(client, EventBody(user.Id, title: Unique("Sin TLS"), participants: ["luis@example.com"])))
            .GetProperty("id").GetGuid();

        var outcome = await ScheduleAsync(client, id);

        Assert.Equal("smtp_unavailable", outcome.Of(id).GetProperty("outlook").GetProperty("errorCode").GetString());
        Assert.Equal(0, await plain.CountAsync(Ct));
        AssertNothingOfTheServerLogged(logs);
    }

    [Fact]
    public async Task RNF1_the_validation_of_production_refuses_a_certificate_it_does_not_trust()
    {
        var logs = new CapturingLoggerProvider();
        var (app, client, user) = await StartAsync(factory: new MailKitSmtpTransportFactory(), logs: logs);
        await using var _ = app;
        using var __ = client;
        var id = (await CreateEventAsync(client, EventBody(user.Id, title: Unique("CA no confiable"))))
            .GetProperty("id").GetGuid();

        var outcome = await ScheduleAsync(client, id);

        Assert.Equal("smtp_unavailable", outcome.Of(id).GetProperty("outlook").GetProperty("errorCode").GetString());
        Assert.Empty(await SentForAsync(id));
        AssertNothingOfTheServerLogged(logs);
    }

    [Fact]
    public async Task RF7_after_the_expiry_the_list_and_the_detail_show_outlook_off()
    {
        var responsible = await UserAsync();
        var id = await SeedEventAsync(responsible.Id, LocalNow().AddDays(20), Unique("Caducada"));
        var (app, client, _) = await StartAsync(clock: new FixedClock(new DateTimeOffset(2027, 5, 6, 0, 0, 0, TimeSpan.Zero)));
        await using var _app = app;
        using var _client = client;

        var detail = await GetEventAsync(client, id);
        var list = JsonDocument.Parse(await client.GetStringAsync(Url("/api/v1/agenda-events?from=2026-01-01&to=2026-02-01"), Ct)).RootElement;

        Assert.False(detail.GetProperty("outlookEnabled").GetBoolean());
        Assert.False(list.GetProperty("outlookEnabled").GetBoolean());
    }

    private static void AssertNothingOfTheServerLogged(CapturingLoggerProvider logs)
    {
        Assert.NotEmpty(logs.Lines);
        Assert.DoesNotContain(logs.Lines, line => line.Contains("MailKit", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Lines, line => line.Contains("@example.com", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Lines, line => line.Contains("placeholder-not-a-secret", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Lines, line => line.Contains("STARTTLS", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs.Lines, line => line.Contains("certificate", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A clock that never moves.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task The_check_refuses_a_list_without_uid_or_empty_or_not_an_array()
    {
        var responsible = await UserAsync();
        var id = await SeedEventAsync(responsible.Id, LocalNow().AddDays(20), Unique("Check"));
        await using var connection = new NpgsqlConnection(Database.ConnectionStringFor(PostgreSqlFixture.AppUser, Database.AppPassword));
        await connection.OpenAsync(Ct);

        async Task<string?> TryAsync(string recipients, string? uid)
        {
            await using var command = new NpgsqlCommand(
                "UPDATE app.agenda_events SET outlook_sent_recipients = @recipients::jsonb, outlook_event_id = @uid WHERE id = @id", connection);
            command.Parameters.AddWithValue("recipients", recipients);
            command.Parameters.AddWithValue("uid", (object?)uid ?? DBNull.Value);
            command.Parameters.AddWithValue("id", id);
            try
            {
                await command.ExecuteNonQueryAsync(Ct);
                return null;
            }
            catch (PostgresException exception)
            {
                return exception.SqlState;
            }
        }

        Assert.Equal(PostgresErrorCodes.CheckViolation, await TryAsync("""["ana@example.com"]""", null));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await TryAsync("[]", $"{id:D}@invite.agenda.itmas"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await TryAsync("""{"a":1}""", $"{id:D}@invite.agenda.itmas"));
        Assert.Null(await TryAsync("""["ana@example.com"]""", $"{id:D}@invite.agenda.itmas"));
    }
}

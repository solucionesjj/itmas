using Itmas3.Api.Agenda;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Infrastructure.Agenda;
using Itmas3.Infrastructure.Agenda.Graph;
using Itmas3.Infrastructure.Agenda.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Itmas3.UnitTests.Api.Agenda;

/// <summary>
/// SPEC-0008 RF-1, RF-7, RF-8 and CA-8 ("configuración"): the start-up validation of the SMTP transport and which port the composition
/// root resolves. Resolving the SMTP adapter connects to nothing: it only connects on the first message.
/// </summary>
public sealed class AgendaSmtpConfigurationTests
{
    private static readonly AgendaOptionsValidator Validator = new();

    private static AgendaOptions Smtp(string? username = "agenda@example.com", string? password = "placeholder-not-a-secret", string? team = "equipo@example.com") => new()
    {
        TimeZone = "America/Bogota",
        PublicUrl = "https://itmas.example.com",
        Outlook = new AgendaOutlookOptions { Enabled = true, TeamAddress = team },
        Smtp = new AgendaSmtpOptions { Username = username, Password = password },
    };

    private static string[] Failures(AgendaOptions options) => [.. Validator.Validate(null, options).Failures ?? []];

    private static ServiceProvider Build(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(setting => setting.Key, setting => (string?)setting.Value))
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddAgendaModule(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static (string, string)[] CompleteSmtp() =>
    [
        ("Agenda:Outlook:Enabled", "true"),
        ("Agenda:Smtp:Username", "Agenda@Example.com"),
        ("Agenda:Smtp:Password", "placeholder-not-a-secret"),
        ("Agenda:PublicUrl", "https://itmas.example.com"),
    ];

    [Fact]
    public void Smtp_is_the_default_transport_and_a_complete_configuration_is_valid()
    {
        Assert.Equal("smtp", new AgendaOutlookOptions().Transport);
        Assert.True(Validator.Validate(null, Smtp()).Succeeded);
    }

    [Theory]
    [InlineData(null, "placeholder-not-a-secret", "Agenda:Smtp:Username is required")]
    [InlineData("agenda@example.com", null, "Agenda:Smtp:Password is required")]
    [InlineData("agenda;x@example.com", "placeholder-not-a-secret", "Agenda:Smtp:Username must be an e-mail address")]
    public void Each_missing_or_wrong_setting_is_reported_by_name_without_its_value(string? username, string? password, string expected)
    {
        var failures = Failures(Smtp(username, password));

        Assert.Contains(failures, failure => failure.StartsWith(expected, StringComparison.Ordinal));
        Assert.DoesNotContain(failures, failure => failure.Contains("placeholder", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("equipo@example.com", true)]
    [InlineData("equipo;x@example.com", false)]
    [InlineData("equipo@otro.example.org", false)]
    public void The_team_list_follows_the_address_rule_and_an_allowed_domain_the_one_of_the_account_by_default(string team, bool valid) =>
        Assert.Equal(valid, Validator.Validate(null, Smtp(team: team)).Succeeded);

    [Fact]
    public void The_public_url_is_required_with_https()
    {
        var options = Smtp();
        options.PublicUrl = "http://itmas.example.com";

        Assert.Contains(Failures(options), failure => failure.StartsWith("Agenda:PublicUrl must be an absolute https URL", StringComparison.Ordinal));
    }

    [Fact]
    public void An_unknown_transport_is_refused_and_graph_does_not_need_the_smtp_settings()
    {
        var unknown = Smtp();
        unknown.Outlook.Transport = "pigeon";
        Assert.Contains(Failures(unknown), failure => failure.StartsWith("Agenda:Outlook:Transport", StringComparison.Ordinal));

        var graph = Smtp(username: null, password: null);
        graph.Outlook.Transport = "graph";
        Assert.DoesNotContain(Failures(graph), failure => failure.Contains("Agenda:Smtp", StringComparison.Ordinal));
    }

    [Fact]
    public void With_smtp_on_the_port_is_the_scoped_smtp_adapter_and_the_pace_is_shared()
    {
        using var provider = Build(CompleteSmtp());
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var gateway = first.ServiceProvider.GetRequiredService<IOutlookCalendarGateway>();
        Assert.IsType<SmtpOutlookCalendarGateway>(gateway);
        Assert.Same(gateway, first.ServiceProvider.GetRequiredService<IOutlookCalendarGateway>());
        Assert.NotSame(gateway, second.ServiceProvider.GetRequiredService<IOutlookCalendarGateway>());
        Assert.Same(provider.GetRequiredService<SmtpThrottleGate>(), provider.GetRequiredService<SmtpThrottleGate>());
    }

    [Fact]
    public void The_composition_of_production_validates_certificates_strictly_and_fixes_host_and_port()
    {
        using var provider = Build(CompleteSmtp());

        var factory = Assert.IsType<MailKitSmtpTransportFactory>(provider.GetRequiredService<ISmtpTransportFactory>());
        Assert.False(factory.UsesTestTrust);
        var settings = provider.GetRequiredService<SmtpOutlookSettings>();
        Assert.Equal(("smtp.office365.com", 587), (settings.Host, settings.Port));
        Assert.Equal("agenda@example.com", settings.Username);
    }

    [Fact]
    public void Without_allowed_domains_the_attendees_are_limited_to_the_domain_of_the_account_and_the_expiry_is_set()
    {
        using var provider = Build(CompleteSmtp());

        var settings = provider.GetRequiredService<AgendaSettings>();
        Assert.Equal(["example.com"], settings.AllowedAttendeeDomains);
        Assert.Equal(OutlookTransport.Smtp, settings.Transport);
        Assert.Equal("agenda@example.com", settings.SenderAddress);
        Assert.Equal(new DateTimeOffset(2027, 5, 6, 0, 0, 0, TimeSpan.Zero), settings.SmtpAcceptedUntil);
        Assert.True(settings.IsOutlookActive(new DateTimeOffset(2027, 5, 5, 23, 59, 59, TimeSpan.Zero)));
        Assert.False(settings.IsOutlookActive(new DateTimeOffset(2027, 5, 6, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void With_graph_the_port_is_still_the_graph_adapter_and_off_calls_nothing()
    {
        using var graph = Build(
            ("Agenda:Outlook:Enabled", "true"), ("Agenda:Outlook:Transport", "graph"),
            ("Agenda:Outlook:TenantId", "33333333-3333-3333-3333-333333333333"), ("Agenda:Outlook:ClientId", "44444444-4444-4444-4444-444444444444"),
            ("Agenda:Outlook:ClientSecret", "placeholder-not-a-secret"), ("Agenda:Outlook:Mailbox", "agenda@example.com"),
            ("Agenda:PublicUrl", "https://itmas.example.com"));
        using var off = Build(("Agenda:Smtp:Username", "agenda@example.com"));
        using var graphScope = graph.CreateScope();
        using var offScope = off.CreateScope();

        Assert.IsType<GraphOutlookCalendarGateway>(graphScope.ServiceProvider.GetRequiredService<IOutlookCalendarGateway>());
        Assert.IsType<NotConfiguredOutlookCalendarGateway>(offScope.ServiceProvider.GetRequiredService<IOutlookCalendarGateway>());
        Assert.False(off.GetRequiredService<AgendaSettings>().IsOutlookActive(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_missing_password_stops_the_start_up()
    {
        using var provider = Build(("Agenda:Outlook:Enabled", "true"), ("Agenda:Smtp:Username", "agenda@example.com"), ("Agenda:PublicUrl", "https://itmas.example.com"));

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgendaOptions>>().Value);
        Assert.Contains("Agenda:Smtp:Password", exception.Message, StringComparison.Ordinal);
    }
}

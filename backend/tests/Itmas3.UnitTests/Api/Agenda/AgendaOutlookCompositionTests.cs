using Itmas3.Api.Agenda;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Infrastructure.Agenda;
using Itmas3.Infrastructure.Agenda.Graph;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Itmas3.UnitTests.Api.Agenda;

/// <summary>
/// SPEC-0006 task 10 (ADR-0016 §9): which implementation of the Outlook port the composition root resolves. Nothing is called:
/// resolving the adapter builds objects but sends nothing to Microsoft.
/// </summary>
public sealed class AgendaOutlookCompositionTests
{
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

    private static (string, string)[] CompleteOutlook() =>
    [
        ("Agenda:Outlook:Enabled", "true"),
        ("Agenda:Outlook:TenantId", "33333333-3333-3333-3333-333333333333"),
        ("Agenda:Outlook:ClientId", "44444444-4444-4444-4444-444444444444"),
        ("Agenda:Outlook:ClientSecret", "placeholder-not-a-secret"),
        ("Agenda:Outlook:Mailbox", "agenda@example.com"),
        ("Agenda:PublicUrl", "https://itmas.example.com"),
    ];

    [Fact]
    public void CA17_with_outlook_off_the_port_is_the_one_that_calls_nothing()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();

        Assert.IsType<NotConfiguredOutlookCalendarGateway>(scope.ServiceProvider.GetRequiredService<IOutlookCalendarGateway>());
    }

    [Fact]
    public void With_outlook_on_the_port_is_the_adapter_of_graph()
    {
        using var provider = Build(CompleteOutlook());
        using var scope = provider.CreateScope();

        Assert.IsType<GraphOutlookCalendarGateway>(scope.ServiceProvider.GetRequiredService<IOutlookCalendarGateway>());
    }

    [Fact]
    public void Q4_without_allowed_domains_the_invitations_are_limited_to_the_domain_of_the_mailbox()
    {
        using var provider = Build(CompleteOutlook());

        Assert.Equal(["example.com"], provider.GetRequiredService<AgendaSettings>().AllowedAttendeeDomains);
    }

    [Fact]
    public void Q4_explicit_domains_replace_the_one_of_the_mailbox_and_with_outlook_off_the_mailbox_adds_none()
    {
        using var withDomains = Build([.. CompleteOutlook(), ("Agenda:AllowedAttendeeDomains", "Corp.Example.com; example.org")]);
        using var outlookOff = Build(("Agenda:Outlook:Mailbox", "agenda@example.com"));

        Assert.Equal(["corp.example.com", "example.org"], withDomains.GetRequiredService<AgendaSettings>().AllowedAttendeeDomains);
        Assert.Empty(outlookOff.GetRequiredService<AgendaSettings>().AllowedAttendeeDomains);
    }

    [Fact]
    public void P26_the_budget_of_notices_is_one_for_the_process_and_says_when_it_renews()
    {
        using var provider = Build(("Agenda:PublishPermitLimitPerHour", "2"));
        var limit = provider.GetRequiredService<IAgendaPublishLimit>();

        Assert.Same(limit, provider.GetRequiredService<IAgendaPublishLimit>());
        Assert.True(limit.TryAcquire(out _));
        Assert.True(limit.TryAcquire(out _));
        Assert.False(limit.TryAcquire(out var retryAfter));
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value, TimeSpan.FromTicks(1), TimeSpan.FromHours(1));
    }

    [Fact]
    public void The_adapter_shares_one_token_provider_and_one_throttle_gate_for_the_whole_process()
    {
        using var provider = Build(CompleteOutlook());

        Assert.Same(provider.GetRequiredService<IGraphTokenProvider>(), provider.GetRequiredService<IGraphTokenProvider>());
        Assert.IsType<MsalGraphTokenProvider>(provider.GetRequiredService<IGraphTokenProvider>());
        Assert.Same(provider.GetRequiredService<GraphThrottleGate>(), provider.GetRequiredService<GraphThrottleGate>());
    }

    [Fact]
    public void The_client_of_the_adapter_has_the_ten_second_limit()
    {
        using var provider = Build(CompleteOutlook());

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GraphOutlookCalendarGateway));

        Assert.Equal(TimeSpan.FromSeconds(10), client.Timeout);
    }

    [Fact]
    public void The_settings_of_the_adapter_come_from_the_validated_options_and_never_print_the_secret()
    {
        using var provider = Build(CompleteOutlook());

        var settings = provider.GetRequiredService<GraphOutlookSettings>();

        Assert.Equal("agenda@example.com", settings.Mailbox);
        Assert.Equal("33333333-3333-3333-3333-333333333333", settings.TenantId);
        Assert.DoesNotContain("placeholder-not-a-secret", settings.ToString(), StringComparison.Ordinal);
    }
}

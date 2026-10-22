using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Infrastructure.Agenda;
using Itmas3.Infrastructure.Agenda.Graph;
using Microsoft.Extensions.Options;

namespace Itmas3.Api.Agenda;

/// <summary>Composition of the infrastructure agenda module (SPEC-0006, ADR-0016).</summary>
internal static class AgendaModuleSetup
{
    public static IServiceCollection AddAgendaModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AgendaOptions>()
            .Bind(configuration.GetSection(AgendaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AgendaOptions>, AgendaOptionsValidator>();

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AgendaOptions>>().Value;
            AgendaOptionsValidator.TryFindZone(options.TimeZone, out var zone);
            var domains = AgendaOptionsValidator.ParseDomains(options.AllowedAttendeeDomains);
            if (domains.Count == 0 && options.Outlook.Enabled && AgendaOptionsValidator.DomainOf(options.Outlook.Mailbox) is { } mailboxDomain)
            {
                domains = [mailboxDomain];
            }

            return new AgendaSettings
            {
                TimeZone = zone,
                AllowedAttendeeDomains = domains,
                PublicUrl = string.IsNullOrWhiteSpace(options.PublicUrl) ? null : options.PublicUrl.Trim(),
                OutlookEnabled = options.Outlook.Enabled,
                TeamAddress = options.Outlook.Enabled && !string.IsNullOrWhiteSpace(options.Outlook.TeamAddress)
                    ? options.Outlook.TeamAddress.Trim()
                    : null,
            };
        });

        AddOutlookGateway(services);

        // The invitations leave the corporate mailbox: one counter for everything that notifies (§14.1, P-26), spent only after
        // authorization by the filter of the routes that notify and by the use cases that change what is already scheduled.
        services.AddSingleton<IAgendaPublishLimit, AgendaPublishLimit>();
        services.AddSingleton<AgendaPublishLimitFilter>();

        return services;
    }

    /// <summary>
    /// The Outlook port (SPEC-0006 task 10, ADR-0016): the adapter of Graph when <c>Agenda:Outlook:Enabled</c> is true and the one that
    /// answers "not configured" otherwise. The choice is made when the port is resolved, from the validated options, so with Outlook
    /// off nothing of Graph or MSAL is ever built or called. The typed client has the default logging of <c>IHttpClientFactory</c>
    /// removed: it would log every URL, and the path of a call carries the address of the mailbox (RNF-8).
    /// </summary>
    private static void AddOutlookGateway(IServiceCollection services)
    {
        services.AddSingleton(serviceProvider =>
        {
            var outlook = serviceProvider.GetRequiredService<IOptions<AgendaOptions>>().Value.Outlook;
            return new GraphOutlookSettings(outlook.TenantId ?? string.Empty, outlook.ClientId ?? string.Empty, outlook.ClientSecret ?? string.Empty, outlook.Mailbox ?? string.Empty);
        });
        services.AddSingleton<GraphThrottleGate>();
        services.AddSingleton<IGraphTokenProvider, MsalGraphTokenProvider>();

        // Typed client of the adapter: only Graph, 10 s, no redirects, no cookies, no logging of the URLs it calls.
        services.AddHttpClient<GraphOutlookCalendarGateway>(client => client.Timeout = GraphOutlookSettings.DefaultRequestTimeout)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            })
            .RemoveAllLoggers();

        services.AddScoped<IOutlookCalendarGateway>(serviceProvider =>
            serviceProvider.GetRequiredService<AgendaSettings>().OutlookEnabled
                ? serviceProvider.GetRequiredService<GraphOutlookCalendarGateway>()
                : new NotConfiguredOutlookCalendarGateway());
    }
}

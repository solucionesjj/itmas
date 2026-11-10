using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Domain.Agenda;
using Itmas3.Infrastructure.Agenda;
using Itmas3.Infrastructure.Agenda.Graph;
using Itmas3.Infrastructure.Agenda.Smtp;
using Microsoft.Extensions.Options;

namespace Itmas3.Api.Agenda;

/// <summary>Composition of the infrastructure agenda module (SPEC-0006, ADR-0016, SPEC-0008, ADR-0023).</summary>
internal static class AgendaModuleSetup
{
    /// <summary>
    /// The end of the accepted exception of the constitution §3.8 for the SMTP credential (SPEC-0008 RF-7, ADR-0023 decision 11): accepted
    /// on 2026-11-06 with review no later than 2027-05-05. From this instant nothing is sent by SMTP and the screens show "Outlook no
    /// configurado". Renewing it needs a new express acceptance and a commit that refers to it; it is not a setting on purpose.
    /// </summary>
    internal static readonly DateTimeOffset SmtpExceptionAcceptedUntil = new(2027, 5, 6, 0, 0, 0, TimeSpan.Zero);

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
            var transport = AgendaOptionsValidator.ParseTransport(options.Outlook.Transport) ?? OutlookTransport.Smtp;
            var sender = options.Outlook.Enabled && transport == OutlookTransport.Smtp ? AgendaEmail.Normalize(options.Smtp.Username) : null;
            var domains = AgendaOptionsValidator.ParseDomains(options.AllowedAttendeeDomains);
            if (domains.Count == 0 && options.Outlook.Enabled)
            {
                // Empty: the domain of the account that sends (SMTP) or of the mailbox (Graph), as before.
                var own = transport == OutlookTransport.Smtp
                    ? sender is null ? null : AgendaEmail.DomainOf(sender)
                    : AgendaOptionsValidator.DomainOf(options.Outlook.Mailbox);
                if (own is not null)
                {
                    domains = [own];
                }
            }

            return new AgendaSettings
            {
                TimeZone = zone,
                AllowedAttendeeDomains = domains,
                PublicUrl = string.IsNullOrWhiteSpace(options.PublicUrl) ? null : options.PublicUrl.Trim(),
                OutlookEnabled = options.Outlook.Enabled,
                Transport = transport,
                SmtpAcceptedUntil = SmtpExceptionAcceptedUntil,
                SenderAddress = sender,
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
    /// The Outlook port: the SMTP adapter (SPEC-0008, ADR-0023) or the Graph one (ADR-0016) when <c>Agenda:Outlook:Enabled</c> is true,
    /// and the one that answers "not configured" otherwise. The choice is made when the port is resolved, from the validated options,
    /// so with Outlook off nothing of SMTP, Graph or MSAL is ever built or called.
    /// <list type="bullet">
    /// <item>SMTP: scoped (one connection per request), with the process-wide pace of <see cref="SmtpThrottleGate"/>, the fixed host and
    /// port, strict certificate validation and no protocol logger or telemetry of MailKit.</item>
    /// <item>Graph: the typed client has the default logging of <c>IHttpClientFactory</c> removed, because it would log every URL, and the
    /// path of a call carries the address of the mailbox (RNF-8).</item>
    /// </list>
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

        services.AddSmtpOutlookGateway(serviceProvider =>
        {
            // Only built when the SMTP transport is in use: the validation at start-up already required both values.
            var smtp = serviceProvider.GetRequiredService<IOptions<AgendaOptions>>().Value.Smtp;
            return new SmtpOutlookSettings(smtp.Username ?? string.Empty, smtp.Password ?? string.Empty);
        });

        services.AddScoped<IOutlookCalendarGateway>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<AgendaSettings>();
            if (!settings.OutlookEnabled)
            {
                return new NotConfiguredOutlookCalendarGateway();
            }

            return settings.Transport == OutlookTransport.Smtp
                ? serviceProvider.GetSmtpOutlookGateway()
                : serviceProvider.GetRequiredService<GraphOutlookCalendarGateway>();
        });
    }
}

using Itmas3.Application.Agenda.Outlook;
using Microsoft.Extensions.DependencyInjection;

namespace Itmas3.Infrastructure.Agenda.Smtp;

/// <summary>
/// Registers the SMTP adapter (SPEC-0008, ADR-0023) for the composition root, which cannot see its internal types: the adapter is
/// scoped (one connection per request), the pace is process-wide and the certificate validation is the strict one of production.
/// </summary>
public static class SmtpOutlookRegistration
{
    public static IServiceCollection AddSmtpOutlookGateway(this IServiceCollection services, Func<IServiceProvider, SmtpOutlookSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);
        services.AddSingleton(settings);
        services.AddSingleton<SmtpThrottleGate>();
        services.AddSingleton<ISmtpTransportFactory>(_ => new MailKitSmtpTransportFactory());
        services.AddScoped<SmtpOutlookCalendarGateway>();
        return services;
    }

    /// <summary>The scoped SMTP adapter of the current request.</summary>
    public static IOutlookCalendarGateway GetSmtpOutlookGateway(this IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<SmtpOutlookCalendarGateway>();
}

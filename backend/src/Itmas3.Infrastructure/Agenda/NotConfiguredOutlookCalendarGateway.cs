using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Outlook;

namespace Itmas3.Infrastructure.Agenda;

/// <summary>
/// The Outlook port when there is no adapter to use: it answers <c>not_configured</c> and calls nothing. With
/// <c>Agenda:Outlook:Enabled=false</c> the use cases never reach it (RF-15); it only prevents an unregistered port.
/// </summary>
public sealed class NotConfiguredOutlookCalendarGateway : IOutlookCalendarGateway
{
    public Task<OutlookGatewayResult> PublishAsync(OutlookEventData data, string? outlookEventId, CancellationToken cancellationToken) =>
        Task.FromResult(OutlookGatewayResult.Failure(OutlookErrorCodes.NotConfigured));

    public Task<OutlookGatewayResult> CancelAsync(string outlookEventId, string reason, CancellationToken cancellationToken) =>
        Task.FromResult(OutlookGatewayResult.Failure(OutlookErrorCodes.NotConfigured));
}

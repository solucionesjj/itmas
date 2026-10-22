using Itmas3.Application.Abstractions.Authorization;
using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Common.Exceptions;

namespace Itmas3.Application.Agenda.Outlook;

/// <summary>
/// Changing something already Scheduled notifies its guests through Outlook, so it needs <c>agenda.publish</c> as well and spends the
/// same budget as scheduling and cancelling (SPEC-0006 P-12, §14.1, P-26; decision of 2026-10-21 on the security review). A draft
/// notifies nobody and stays with <c>agenda.update</c>. The check runs inside the use case, where the status is read in the same
/// transaction as the change, so a draft scheduled meanwhile cannot slip through.
/// </summary>
public sealed class AgendaNotificationGuard(
    ICurrentUser currentUser, IPermissionEvaluator permissions, IAgendaPublishLimit limit)
{
    /// <summary>Throws <see cref="ForbiddenException"/> without <c>agenda.publish</c> and <see cref="TooManyRequestsException"/> when the budget is spent.</summary>
    public async Task EnsureCanNotifyAsync(CancellationToken cancellationToken)
    {
        if (!await permissions.IsGrantedAsync(currentUser, AgendaPermissions.Publish, cancellationToken))
        {
            throw new ForbiddenException(
                "Modificar una actividad programada avisa a sus invitados: necesitas también el permiso agenda.publish.");
        }

        if (!limit.TryAcquire(out var retryAfter))
        {
            throw new TooManyRequestsException(
                "Se alcanzó el límite de avisos por hora de la agenda. Inténtalo más tarde.", retryAfter);
        }
    }
}

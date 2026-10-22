using System.Threading.RateLimiting;
using Itmas3.Application.Agenda.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Itmas3.Api.Agenda;

/// <summary>
/// The shared budget of the operations that notify people through Outlook (SPEC-0006 §14.1, P-26): one fixed window of
/// <c>AGENDA_PUBLISH_REQUESTS_PER_HOUR</c> for the whole process. It is not a rate-limiting policy of the pipeline on purpose: the
/// pipeline limits before authorization, and then anonymous or unauthorized requests would spend the budget of everyone (security
/// review of 2026-10-21, F-1). Here it is taken by <see cref="AgendaPublishLimitFilter"/> and by the use cases, after authorization.
/// </summary>
internal sealed class AgendaPublishLimit : IAgendaPublishLimit, IDisposable
{
    private readonly FixedWindowRateLimiter limiter;

    public AgendaPublishLimit(IOptions<AgendaOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = options.Value.PublishPermitLimitPerHour,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }

    public bool TryAcquire(out TimeSpan? retryAfter)
    {
        using var lease = limiter.AttemptAcquire();
        retryAfter = !lease.IsAcquired && lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : null;
        return lease.IsAcquired;
    }

    public void Dispose() => limiter.Dispose();
}

/// <summary>
/// Spends one permit of <see cref="AgendaPublishLimit"/> for scheduling, cancelling and synchronizing. MVC runs action filters after
/// authentication, authorization and the CSRF check, so only requests that are going to notify count; over the budget the answer is
/// a <c>429</c> with <c>Retry-After</c> (see the global exception handler).
/// </summary>
internal sealed class AgendaPublishLimitFilter(IAgendaPublishLimit limit) : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return limit.TryAcquire(out var retryAfter)
            ? next()
            : throw new TooManyRequestsException("Se alcanzó el límite de avisos por hora de la agenda. Inténtalo más tarde.", retryAfter);
    }
}

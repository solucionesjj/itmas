using Itmas3.Agent.Configuration;

namespace Itmas3.Agent.Scheduling;

/// <summary>
/// Sending cadence (SPEC-0002 RF-8, RF-10): at start-up if 24 h or more have passed since the last success; then every
/// 24 h ± 30 min; failures retry with exponential backoff (1, 2, 4… 60 min); a rejected credential is retried daily.
/// </summary>
public static class SchedulePolicy
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    public static readonly TimeSpan MaxJitter = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(60);

    public static DateTimeOffset NextRunAtStartup(AgentState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.CredentialRejectedAt is { } rejected && (state.LastSuccessAt is null || rejected > state.LastSuccessAt))
        {
            return Max(now, rejected + Interval);
        }

        if (state.LastSuccessAt is not { } lastSuccess || now - lastSuccess >= Interval)
        {
            return now;
        }

        return state.NextRunAt ?? lastSuccess + Interval;
    }

    public static AgentState AfterSuccess(AgentState state, DateTimeOffset now, TimeSpan jitter) =>
        state with
        {
            LastSuccessAt = now,
            LastAttemptAt = now,
            NextRunAt = now + Interval + Clamp(jitter),
            ConsecutiveFailures = 0,
            CredentialRejectedAt = null,
            LastError = null,
        };

    public static AgentState AfterFailure(AgentState state, DateTimeOffset now, string error)
    {
        ArgumentNullException.ThrowIfNull(state);
        var failures = state.ConsecutiveFailures + 1;
        return state with
        {
            LastAttemptAt = now,
            NextRunAt = now + Backoff(failures),
            ConsecutiveFailures = failures,
            LastError = error,
        };
    }

    public static AgentState AfterCredentialRejected(AgentState state, DateTimeOffset now) =>
        state with
        {
            LastAttemptAt = now,
            NextRunAt = now + Interval,
            CredentialRejectedAt = now,
            LastError = "La API rechazó la credencial del dispositivo (401). Reinstale el agente para enrolarlo de nuevo.",
        };

    public static TimeSpan Backoff(int consecutiveFailures)
    {
        var minutes = Math.Pow(2, Math.Clamp(consecutiveFailures - 1, 0, 10));
        return TimeSpan.FromMinutes(Math.Min(minutes, MaxBackoff.TotalMinutes));
    }

    /// <summary>Uniform random offset in [-30 min, +30 min] so devices do not all send at the same time.</summary>
    public static TimeSpan RandomJitter(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return TimeSpan.FromSeconds(random.Next(-(int)MaxJitter.TotalSeconds, (int)MaxJitter.TotalSeconds + 1));
    }

    private static TimeSpan Clamp(TimeSpan jitter) =>
        jitter > MaxJitter ? MaxJitter : jitter < -MaxJitter ? -MaxJitter : jitter;

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left > right ? left : right;
}

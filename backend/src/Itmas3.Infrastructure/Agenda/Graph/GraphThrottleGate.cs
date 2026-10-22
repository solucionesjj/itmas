namespace Itmas3.Infrastructure.Agenda.Graph;

/// <summary>
/// Remembers that Graph asked to wait (<c>Retry-After</c>, SPEC-0006 RNF-5) so that no call is made before the end of that
/// wait, not even from another request. One instance for the whole process. There are no automatic retries: the people retry.
/// </summary>
public sealed class GraphThrottleGate(TimeProvider timeProvider)
{
    /// <summary>Used when Graph throttles without saying for how long.</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(30);

    /// <summary>A longer request is not obeyed in full: a wrong header must not stop the publication for hours.</summary>
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(10);

    private readonly Lock _lock = new();
    private DateTimeOffset _until = DateTimeOffset.MinValue;

    /// <summary>True when a previous answer asked to wait and the wait has not ended; <paramref name="remaining"/> is what is left.</summary>
    public bool IsClosed(out TimeSpan remaining)
    {
        lock (_lock)
        {
            remaining = _until - timeProvider.GetUtcNow();
            return remaining > TimeSpan.Zero;
        }
    }

    /// <summary>Closes the gate for <paramref name="delay"/> (the default when it is null or not positive), up to <see cref="MaxDelay"/>.</summary>
    public void Close(TimeSpan? delay)
    {
        var wait = delay is { } requested && requested > TimeSpan.Zero ? requested : DefaultDelay;
        var until = timeProvider.GetUtcNow() + (wait > MaxDelay ? MaxDelay : wait);
        lock (_lock)
        {
            if (until > _until)
            {
                _until = until;
            }
        }
    }
}

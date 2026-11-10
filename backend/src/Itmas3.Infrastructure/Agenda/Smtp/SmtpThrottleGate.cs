namespace Itmas3.Infrastructure.Agenda.Smtp;

/// <summary>
/// The pace of the SMTP sends for the whole API process (SPEC-0008 RF-6, ADR-0023 decision 7), below the limits of Exchange Online
/// (30 messages a minute and 3 connections per mailbox): at most <see cref="MessagesPerMinute"/> messages in any minute and
/// <see cref="MaxConnections"/> connections at a time. After a refused authentication it pauses every send for
/// <see cref="AuthenticationPause"/>, so that a stale password or an attack do not lock the account with repeated failures.
/// </summary>
public sealed class SmtpThrottleGate(TimeProvider timeProvider)
{
    public const int MessagesPerMinute = 25;
    public const int MaxConnections = 2;
    public static readonly TimeSpan AuthenticationPause = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _connections = new(MaxConnections, MaxConnections);
    private readonly Queue<DateTimeOffset> _sent = new();
    private readonly Lock _lock = new();
    private DateTimeOffset? _pausedUntil;

    /// <summary>Whether sends are paused after a refused authentication.</summary>
    public bool IsPaused
    {
        get
        {
            lock (_lock)
            {
                return _pausedUntil is { } until && timeProvider.GetUtcNow() < until;
            }
        }
    }

    /// <summary>Stops every send of the process for <see cref="AuthenticationPause"/>.</summary>
    public void PauseAfterAuthenticationFailure()
    {
        lock (_lock)
        {
            _pausedUntil = timeProvider.GetUtcNow() + AuthenticationPause;
        }
    }

    /// <summary>How long to wait before the next message fits in the pace; zero when it fits now.</summary>
    public TimeSpan DelayBeforeNextMessage()
    {
        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();
            Prune(now);
            return _sent.Count < MessagesPerMinute ? TimeSpan.Zero : _sent.Peek() + Window - now;
        }
    }

    /// <summary>Counts a message the server accepted or refused (it was submitted either way).</summary>
    public void RecordMessage()
    {
        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();
            Prune(now);
            _sent.Enqueue(now);
        }
    }

    /// <summary>Takes a connection slot, waiting at most <paramref name="maxWait"/>. False when none was free in time.</summary>
    public Task<bool> TryEnterConnectionAsync(TimeSpan maxWait, CancellationToken cancellationToken) =>
        _connections.WaitAsync(maxWait > TimeSpan.Zero ? maxWait : TimeSpan.Zero, cancellationToken);

    public void ReleaseConnection() => _connections.Release();

    private void Prune(DateTimeOffset now)
    {
        while (_sent.Count > 0 && _sent.Peek() + Window <= now)
        {
            _sent.Dequeue();
        }
    }
}

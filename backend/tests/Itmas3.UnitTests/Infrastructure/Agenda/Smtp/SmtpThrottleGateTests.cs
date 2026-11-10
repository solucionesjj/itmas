using Itmas3.Infrastructure.Agenda.Smtp;
using Itmas3.UnitTests.Application.Agenda;

namespace Itmas3.UnitTests.Infrastructure.Agenda.Smtp;

/// <summary>SPEC-0008 RF-6, CA-8: the process-wide pace of the SMTP sends (ADR-0023 decision 7).</summary>
public sealed class SmtpThrottleGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 15, 0, 0, TimeSpan.Zero);

    private readonly AdjustableClock _clock = new(Now);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Twenty_five_messages_fit_in_a_minute_and_the_26th_waits_until_the_oldest_leaves_the_window()
    {
        var gate = new SmtpThrottleGate(_clock);
        for (var i = 0; i < 25; i++)
        {
            Assert.Equal(TimeSpan.Zero, gate.DelayBeforeNextMessage());
            gate.RecordMessage();
            _clock.Set(_clock.GetUtcNow().AddSeconds(1));
        }

        Assert.Equal(TimeSpan.FromSeconds(35), gate.DelayBeforeNextMessage());
        _clock.Set(Now.AddMinutes(1));
        Assert.Equal(TimeSpan.Zero, gate.DelayBeforeNextMessage());
    }

    [Fact]
    public async Task At_most_two_connections_at_a_time()
    {
        var gate = new SmtpThrottleGate(_clock);

        Assert.True(await gate.TryEnterConnectionAsync(TimeSpan.Zero, Ct));
        Assert.True(await gate.TryEnterConnectionAsync(TimeSpan.Zero, Ct));
        Assert.False(await gate.TryEnterConnectionAsync(TimeSpan.Zero, Ct));
        gate.ReleaseConnection();
        Assert.True(await gate.TryEnterConnectionAsync(TimeSpan.FromSeconds(-1), Ct));
    }

    [Fact]
    public void A_refused_authentication_pauses_every_send_for_15_minutes()
    {
        var gate = new SmtpThrottleGate(_clock);

        gate.PauseAfterAuthenticationFailure();

        Assert.True(gate.IsPaused);
        _clock.Set(Now.AddMinutes(15).AddTicks(-1));
        Assert.True(gate.IsPaused);
        _clock.Set(Now.AddMinutes(15));
        Assert.False(gate.IsPaused);
    }
}

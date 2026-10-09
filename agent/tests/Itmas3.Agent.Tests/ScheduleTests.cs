using Itmas3.Agent.Configuration;
using Itmas3.Agent.Scheduling;

namespace Itmas3.Agent.Tests;

public sealed class ScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CA05_first_start_runs_immediately() =>
        Assert.Equal(Now, SchedulePolicy.NextRunAtStartup(new AgentState(), Now));

    [Fact]
    public void CA05_start_after_24_hours_runs_immediately() =>
        Assert.Equal(Now, SchedulePolicy.NextRunAtStartup(new AgentState(LastSuccessAt: Now.AddHours(-25), NextRunAt: Now.AddHours(1)), Now));

    [Fact]
    public void CA05_start_within_24_hours_keeps_the_planned_run() =>
        Assert.Equal(Now.AddHours(10), SchedulePolicy.NextRunAtStartup(new AgentState(LastSuccessAt: Now.AddHours(-14), NextRunAt: Now.AddHours(10)), Now));

    [Fact]
    public void CA05_after_success_the_next_run_is_24_hours_plus_jitter()
    {
        var state = SchedulePolicy.AfterSuccess(new AgentState(ConsecutiveFailures: 3, LastError: "x"), Now, TimeSpan.FromMinutes(-12));

        Assert.Equal(Now.AddHours(24).AddMinutes(-12), state.NextRunAt);
        Assert.Equal(0, state.ConsecutiveFailures);
        Assert.Null(state.LastError);
    }

    [Fact]
    public void Jitter_is_bounded_to_30_minutes()
    {
        var random = new Random(42);
        for (var i = 0; i < 1000; i++)
        {
            var jitter = SchedulePolicy.RandomJitter(random);
            Assert.InRange(jitter, TimeSpan.FromMinutes(-30), TimeSpan.FromMinutes(30));
        }

        Assert.Equal(Now.AddHours(24).AddMinutes(30), SchedulePolicy.AfterSuccess(new AgentState(), Now, TimeSpan.FromHours(5)).NextRunAt);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(6, 32)]
    [InlineData(7, 60)]
    [InlineData(50, 60)]
    public void CA06_failures_back_off_exponentially_up_to_an_hour(int failures, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), SchedulePolicy.Backoff(failures));

    [Fact]
    public void CA06_each_failure_schedules_the_next_backoff()
    {
        var state = new AgentState(LastSuccessAt: Now.AddDays(-1));
        state = SchedulePolicy.AfterFailure(state, Now, "down");
        state = SchedulePolicy.AfterFailure(state, Now.AddMinutes(1), "down");

        Assert.Equal(2, state.ConsecutiveFailures);
        Assert.Equal(Now.AddMinutes(3), state.NextRunAt);
    }

    [Fact]
    public void CA13_rejected_credentials_are_retried_daily_even_after_restart()
    {
        var state = SchedulePolicy.AfterCredentialRejected(new AgentState(LastSuccessAt: Now.AddDays(-3)), Now);

        Assert.Equal(Now.AddHours(24), state.NextRunAt);
        Assert.Equal(Now.AddHours(24), SchedulePolicy.NextRunAtStartup(state, Now.AddMinutes(5)));
    }
}

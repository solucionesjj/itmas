using Itmas3.Agent.Api;
using Itmas3.Agent.Configuration;
using Itmas3.Agent.Service;
using Microsoft.Extensions.Logging.Abstractions;

namespace Itmas3.Agent.Tests;

public sealed class InventoryCycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Accepted_reports_schedule_the_next_day()
    {
        var store = Enrolled();
        var api = new RecordingApiClient();

        var state = await Cycle(api, store).RunAsync(new AgentState(), Fixtures.Ct);

        Assert.Single(api.Submitted);
        Assert.Equal(Now, state.LastSuccessAt);
        Assert.InRange(state.NextRunAt!.Value, Now.AddHours(23.5), Now.AddHours(24.5));
        Assert.Equal(state, store.State);
    }

    [Fact]
    public async Task CA13_rejected_credentials_stop_sending_until_the_next_day()
    {
        var store = Enrolled();

        var state = await Cycle(new RecordingApiClient { SubmitResult = SubmitOutcome.CredentialRejected }, store).RunAsync(new AgentState(), Fixtures.Ct);

        Assert.Equal(Now, state.CredentialRejectedAt);
        Assert.Equal(Now.AddHours(24), state.NextRunAt);
        Assert.Contains("Reinstale", state.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CA06_failures_are_retried_with_backoff()
    {
        var state = await Cycle(new RecordingApiClient { SubmitResult = SubmitOutcome.Failed }, Enrolled()).RunAsync(new AgentState(), Fixtures.Ct);

        Assert.Equal(1, state.ConsecutiveFailures);
        Assert.Equal(Now.AddMinutes(1), state.NextRunAt);
    }

    [Fact]
    public async Task Not_enrolled_agents_do_not_collect()
    {
        var api = new RecordingApiClient();

        var state = await Cycle(api, new InMemoryStore()).RunAsync(new AgentState(), Fixtures.Ct);

        Assert.Empty(api.Submitted);
        Assert.Contains("no está enrolado", state.LastError, StringComparison.Ordinal);
    }

    private static InMemoryStore Enrolled() => new()
    {
        Configuration = new AgentConfiguration("https://itmas.example.test"),
        ApiKey = "device-api-key-placeholder",
    };

    private static InventoryCycle Cycle(RecordingApiClient api, InMemoryStore store) =>
        new(ContractTests.Collector(new FakePlatform()), api, store, new FakeTimeProvider(Now), new Random(1), NullLogger<InventoryCycle>.Instance);
}

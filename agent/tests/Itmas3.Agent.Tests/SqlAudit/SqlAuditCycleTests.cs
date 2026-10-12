using Itmas3.Agent.Api;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Configuration;
using Itmas3.Agent.Service;
using Itmas3.Agent.SqlAudit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Itmas3.Agent.Tests.SqlAudit;

/// <summary>
/// Daily SQL audit cycle (SPEC-0005 RF-1, RF-7, RF-8; CA-1, CA-2, CA-6): one snapshot per connection with its own
/// submissionId, reused by the HTTP retries of that snapshot; backoff and re-collection on failure; never breaks the
/// inventory; no engine connection without configured connections; no secrets or names in the logs.
/// </summary>
public sealed class SqlAuditCycleTests : IDisposable
{
    private const string Password = "pw placeholder-not-a-secret";
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly string _root = Directory.CreateTempSubdirectory("itmas-agent-sql-cycle").FullName;
    private readonly SqlConnectionStore _connections;
    private readonly FakeSqlEngine _engine = new();
    private readonly RecordingApiClient _api = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly CapturingLogger<SqlAuditCycle> _cycleLogger = new();
    private readonly CapturingLogger<SqlAuditCollector> _collectorLogger = new();
    private int _engineCreations;

    public SqlAuditCycleTests()
    {
        _connections = new SqlConnectionStore(new AgentPaths(_root, Path.Combine(_root, "itmas-agent"), Path.Combine(_root, "data"), Path.Combine(_root, "data"), null));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task CA02_without_connections_no_engine_is_ever_contacted()
    {
        await Cycle().RunAsync(new AgentState(), inventoryRan: true, Fixtures.Ct);

        Assert.Equal(0, _engineCreations);
        Assert.Empty(_api.SqlSubmitted);
        Assert.Null(Cycle().NextRetryAt());
        Assert.False(Directory.Exists(Path.Combine(_root, "data", "sql")));
    }

    [Fact]
    public async Task CA01_each_connection_sends_one_snapshot_with_its_own_submission_id()
    {
        Add("uno");
        Add("dos");

        await Cycle().RunAsync(new AgentState(), inventoryRan: true, Fixtures.Ct);

        Assert.Equal(["dos", "uno"], _api.SqlSubmitted.Select(snapshot => snapshot.Connection.Alias).Order());
        Assert.Equal(2, _api.SqlSubmitted.Select(snapshot => snapshot.SubmissionId).Distinct().Count());
        Assert.All(_engine.Collections, collection => Assert.Equal(Password, collection.Password));
        Assert.All(_connections.LoadResults().Values, result => Assert.Equal((SqlOutcomes.Complete, Now, (DateTimeOffset?)null), (result.LastOutcome, result.LastSuccessAt!.Value, result.NextRetryAt)));
    }

    [Fact]
    public async Task RF08_http_retries_of_a_snapshot_reuse_its_submission_id()
    {
        Add("uno");
        _api.SqlOutcomes.Enqueue(SubmitOutcome.TransientFailure);
        _api.SqlOutcomes.Enqueue(SubmitOutcome.TransientFailure);

        await Cycle().RunAsync(new AgentState(), inventoryRan: true, Fixtures.Ct);

        Assert.Equal(3, _api.SqlSubmitted.Count);
        Assert.Single(_api.SqlSubmitted.Select(snapshot => snapshot.SubmissionId).Distinct());
        Assert.Single(_engine.Collections);
        Assert.Equal(SqlOutcomes.Complete, _connections.LoadResults().Values.Single().LastOutcome);
    }

    [Fact]
    public async Task RF08_failed_sends_back_off_and_the_retry_collects_a_new_snapshot()
    {
        var connection = Add("uno");
        _api.SqlOutcomes.Enqueue(SubmitOutcome.Failed);

        await Cycle().RunAsync(new AgentState(), inventoryRan: true, Fixtures.Ct);

        var failed = _connections.LoadResults()[connection.Id];
        Assert.Equal((SqlOutcomes.Failed, 1, Now.AddMinutes(1)), (failed.LastOutcome, failed.ConsecutiveFailures, failed.NextRetryAt!.Value));
        Assert.Equal(Now.AddMinutes(1), Cycle().NextRetryAt());

        await Cycle().RunAsync(new AgentState(), inventoryRan: false, Fixtures.Ct); // not due yet
        Assert.Single(_engine.Collections);

        _time.Now = Now.AddMinutes(1);
        await Cycle().RunAsync(new AgentState(), inventoryRan: false, Fixtures.Ct);
        Assert.Equal(2, _engine.Collections.Count);
        Assert.Equal(2, _api.SqlSubmitted.Select(snapshot => snapshot.SubmissionId).Distinct().Count());
        Assert.Null(_connections.LoadResults()[connection.Id].NextRetryAt);
    }

    [Fact]
    public async Task Inventory_retries_do_not_resend_a_snapshot_sent_today()
    {
        Add("uno");
        await Cycle().RunAsync(new AgentState(), inventoryRan: true, Fixtures.Ct);

        _time.Now = Now.AddHours(2);
        await Cycle().RunAsync(new AgentState(), inventoryRan: true, Fixtures.Ct);
        Assert.Single(_api.SqlSubmitted);

        _time.Now = Now.AddHours(24);
        await Cycle().RunAsync(new AgentState(), inventoryRan: true, Fixtures.Ct);
        Assert.Equal(2, _api.SqlSubmitted.Count);
    }

    [Fact]
    public async Task CA06_an_unreachable_instance_is_still_sent_with_the_reason()
    {
        Add("uno");
        _engine.CollectFailure = new SqlAuditException(SqlFailureKind.Authentication, "Login failed (SqlException 18456).");

        await Cycle().RunAsync(new AgentState(), inventoryRan: true, Fixtures.Ct);

        var snapshot = Assert.Single(_api.SqlSubmitted);
        Assert.Null(snapshot.Instance);
        Assert.Equal([new SqlUnavailableSection(SqlSections.Instance, SqlUnavailableReasons.AuthenticationFailed)], snapshot.Unavailable);
        Assert.Equal(SqlOutcomes.Unreachable, _connections.LoadResults().Values.Single().LastOutcome);
    }

    [Fact]
    public async Task A_rejected_device_credential_stops_the_remaining_connections()
    {
        Add("uno");
        Add("dos");
        _api.SqlOutcomes.Enqueue(SubmitOutcome.CredentialRejected);

        await Cycle().RunAsync(new AgentState(), inventoryRan: true, Fixtures.Ct);

        Assert.Single(_api.SqlSubmitted);
        Assert.Null(Cycle().NextRetryAt());
    }

    [Fact]
    public async Task Nothing_is_collected_while_the_inventory_has_the_credential_rejected()
    {
        Add("uno");

        await Cycle().RunAsync(new AgentState(CredentialRejectedAt: Now), inventoryRan: true, Fixtures.Ct);

        Assert.Equal(0, _engineCreations);
        Assert.Empty(_api.SqlSubmitted);
    }

    [Fact]
    public async Task RF01_a_sql_audit_failure_never_breaks_the_inventory()
    {
        Add("uno");
        _engine.CollectFailure = new InvalidOperationException("unexpected bug");
        var store = Enrolled();
        var inventory = new InventoryCycle(ContractTests.Collector(new FakePlatform()), _api, store, _time, new Random(1), NullLogger<InventoryCycle>.Instance);

        var state = await inventory.RunAsync(new AgentState(), Fixtures.Ct);
        await Cycle(store).RunAsync(state, inventoryRan: true, Fixtures.Ct);

        Assert.Single(_api.Submitted);
        Assert.Equal(Now, store.State.LastSuccessAt);
        Assert.Empty(_api.SqlSubmitted);
        Assert.Contains(_cycleLogger.Messages, message => message.Contains("inventory is not affected", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Logs_never_contain_passwords_or_account_and_database_names()
    {
        Add("uno");
        Add("dos");
        _api.SqlOutcomes.Enqueue(SubmitOutcome.Failed);

        await Cycle().RunAsync(new AgentState(), inventoryRan: true, Fixtures.Ct);
        _engine.CollectFailure = new SqlAuditException(SqlFailureKind.Permission, "SQL Server catalog read failed (SqlException 229).");
        _time.Now = Now.AddDays(1);
        await Cycle().RunAsync(new AgentState(), inventoryRan: true, Fixtures.Ct);

        var logs = string.Join('\n', _cycleLogger.Messages.Concat(_collectorLogger.Messages));
        Assert.NotEmpty(logs);
        foreach (var secret in new[] { Password, "itmas_agent", "cuenta_confidencial", "Ventas_Secretas" })
        {
            Assert.DoesNotContain(secret, logs, StringComparison.Ordinal);
        }
    }

    private SqlConnectionDefinition Add(string alias)
    {
        var connection = new SqlConnectionDefinition(Guid.NewGuid(), alias, SqlEngines.SqlServer, "sql01.example.test", 1433, null, null, "itmas_agent", SqlTlsModes.Verify, null, Now, Now);
        _connections.Save(connection, Password);
        return connection;
    }

    private static InMemoryStore Enrolled() => new()
    {
        Configuration = new AgentConfiguration("https://itmas.example.test"),
        ApiKey = "device-api-key-placeholder",
    };

    private SqlAuditCycle Cycle(InMemoryStore? store = null)
    {
        var collector = new SqlAuditCollector(_ => { _engineCreations++; return _engine; }, _time, _collectorLogger);
        return new SqlAuditCycle(_connections, collector, _api, store ?? Enrolled(), _time, _cycleLogger, sendRetryDelays: [TimeSpan.Zero, TimeSpan.Zero]);
    }
}

using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.Model;

namespace Itmas3.Agent.Tests.SqlAudit;

/// <summary>Contract rules enforced while assembling a snapshot (SPEC-0005 §12.2, contract §1, RNF-8, CA-17).</summary>
public sealed class SqlSnapshotAssemblerTests
{
    private static readonly SqlInstanceInfo Instance = new("1.0", null, SqlAuthenticationModes.Password);

    [Fact]
    public void Engine_text_is_sanitized_like_the_inventory()
    {
        var longName = new string('x', 300);
        var snapshot = Assemble(Server(
            [Account("k1", "con\u0007trol\u0000es "), Account("k2", longName), Account("k3", "\u0001\u0002 ")],
            permissions: [new CatalogPermission("k1", Securables.Server(), " view\u0007 server state ", SqlPermissionStates.Grant, null)]));

        Assert.Equal(["controles", new string('x', 256)], snapshot.Accounts.Select(account => account.Name));
        Assert.Equal("VIEW SERVER STATE", Assert.Single(snapshot.Permissions).Permission);
        Assert.DoesNotContain(snapshot.Accounts, account => account.Name.Any(char.IsControl));
    }

    [Fact]
    public void Anonymous_mysql_accounts_keep_their_empty_name_and_other_empty_names_are_dropped()
    {
        var snapshot = Assemble(Server(
            [Account("k1", string.Empty, type: SqlAccountTypes.MySqlAccount), Account("k2", string.Empty), Account("k3", "  ", type: SqlAccountTypes.MySqlAccount)]));

        var anonymous = Assert.Single(snapshot.Accounts);
        Assert.Equal(string.Empty, anonymous.Name);
        Assert.Equal(SqlAccountTypes.MySqlAccount, anonymous.Type);
    }

    [Fact]
    public void Refs_follow_the_contract_pattern_and_duplicates_are_merged_by_identity()
    {
        var snapshot = Assemble(Server(
            [Account("k1", "app"), Account("k2", "app"), Account("k3", "app", type: SqlAccountTypes.ServerRole), Account("k4", "rol", type: SqlAccountTypes.ServerRole)],
            [new CatalogMembership("k1", "k4"), new CatalogMembership("k2", "k4"), new CatalogMembership("k1", "k1")],
            [
                new CatalogPermission("k1", Securables.Server(), "CONNECT SQL", SqlPermissionStates.Grant, null),
                new CatalogPermission("k2", Securables.Server(), "CONNECT SQL", SqlPermissionStates.Grant, "k9"),
                new CatalogPermission("k2", Securables.Server(), "CONNECT SQL", SqlPermissionStates.Deny, null),
            ]));

        Assert.Equal(["a1", "a2", "a3"], snapshot.Accounts.Select(account => account.Ref));
        Assert.All(snapshot.Accounts, account => Assert.Matches("^[A-Za-z0-9_-]{1,16}$", account.Ref));
        Assert.Single(snapshot.RoleMemberships);
        Assert.Equal([SqlPermissionStates.Grant, SqlPermissionStates.Deny], snapshot.Permissions.Select(permission => permission.State));
        Assert.Null(snapshot.Permissions[0].GrantorRef);
    }

    [Fact]
    public void Elements_that_break_contract_rules_are_dropped_not_invented()
    {
        var snapshot = Assemble(
            Server(
                [Account("k1", "app"), Account("k2", "user"), Account("db", "u", level: SqlAccountLevels.Database, database: "no-existe")],
                [new CatalogMembership("k1", "k2"), new CatalogMembership("k1", "desconocida")],
                [
                    new CatalogPermission("k1", new SqlSecurableInfo(SqlSecurableClasses.Database, null, null, null, null, null), "CONNECT", SqlPermissionStates.Grant, null),
                    new CatalogPermission("k1", new SqlSecurableInfo(SqlSecurableClasses.Object, "db", "s", null, "table", null), "SELECT", SqlPermissionStates.Grant, null),
                    new CatalogPermission("k1", new SqlSecurableInfo(SqlSecurableClasses.Server, null, null, "x", null, null), "ALTER", SqlPermissionStates.Grant, null),
                    new CatalogPermission("k1", Securables.Server(), "1BAD", SqlPermissionStates.Grant, null),
                    new CatalogPermission("k1", Securables.Server(), "ALTER", "revoke", null),
                    new CatalogPermission("nadie", Securables.Server(), "ALTER", SqlPermissionStates.Grant, null),
                    new CatalogPermission("k1", Securables.Server(), "ALTER ANY LOGIN", SqlPermissionStates.Grant, null),
                ]),
            databases: [new CatalogDatabase("db", SqlDatabaseStates.Online, "k1")]);

        Assert.Equal(["app", "user"], snapshot.Accounts.Select(account => account.Name)); // database account of an unknown database
        Assert.Empty(snapshot.RoleMemberships); // "user" is not a role; unknown role
        Assert.Equal("ALTER ANY LOGIN", Assert.Single(snapshot.Permissions).Permission);
        Assert.Equal("a1", Assert.Single(snapshot.Databases).OwnerRef);
    }

    [Fact]
    public void Login_refs_only_point_to_server_accounts()
    {
        var catalog = new SqlCatalog(Instance, [new CatalogDatabase("db", SqlDatabaseStates.Online, null)],
        [
            Server([Account("s:1", "login")]),
            new CatalogSection("database:db",
                [
                    Account("d:1", "user", SqlAccountLevels.Database, "db", loginKey: "s:1"),
                    Account("d:2", "other", SqlAccountLevels.Database, "db", loginKey: "d:1"),
                ], [], [], 0),
        ], []);

        var snapshot = SqlFixtures.Snapshot(SqlEngines.SqlServer, catalog);

        Assert.Equal("a1", snapshot.Accounts.Single(account => account.Name == "user").LoginRef);
        Assert.Null(snapshot.Accounts.Single(account => account.Name == "other").LoginRef);
    }

    [Fact]
    public void Databases_over_the_limits_are_reported_unavailable_instead_of_truncated()
    {
        var big = Enumerable.Range(0, SqlSnapshotAssembler.MaxPermissions)
            .Select(index => new CatalogPermission("s:1", Securables.Object("grande", null, $"t{index}", SqlObjectTypes.Table), "SELECT", SqlPermissionStates.Grant, null))
            .ToList();
        var catalog = new SqlCatalog(Instance,
            [new CatalogDatabase("grande", SqlDatabaseStates.Online, null), new CatalogDatabase("chica", SqlDatabaseStates.Online, null)],
            [
                Server([Account("s:1", "app")], permissions: [new CatalogPermission("s:1", Securables.Server(), "CONNECT", SqlPermissionStates.Grant, null)]),
                new CatalogSection("database:grande", [], [], big, 0),
                new CatalogSection("database:chica", [], [], [new CatalogPermission("s:1", Securables.Database("chica"), "CONNECT", SqlPermissionStates.Grant, null)], 2),
            ], []);

        var snapshot = SqlFixtures.Snapshot(SqlEngines.PostgreSql, catalog);

        Assert.Equal([new SqlUnavailableSection("database:grande", SqlUnavailableReasons.Error)], snapshot.Unavailable);
        Assert.Equal(2, snapshot.Permissions.Count);
        Assert.Equal(2, snapshot.Omitted.BuiltinPublicPermissions);
    }

    [Fact]
    public void Unavailable_sections_are_unique_and_skip_their_elements()
    {
        var catalog = new SqlCatalog(Instance, [new CatalogDatabase("db", SqlDatabaseStates.Online, null)],
        [
            Server([Account("s:1", "app")]),
            new CatalogSection("database:db", [Account("d:1", "user", SqlAccountLevels.Database, "db")], [], [], 0),
        ],
        [
            new SqlUnavailableSection("database:db", SqlUnavailableReasons.Timeout),
            new SqlUnavailableSection("database:db", SqlUnavailableReasons.Error),
            new SqlUnavailableSection("database:otra", SqlUnavailableReasons.Error),
        ]);

        var snapshot = SqlFixtures.Snapshot(SqlEngines.SqlServer, catalog);

        Assert.Equal([new SqlUnavailableSection("database:db", SqlUnavailableReasons.Timeout)], snapshot.Unavailable);
        Assert.Equal(["app"], snapshot.Accounts.Select(account => account.Name));
    }

    [Fact]
    public void An_unreadable_instance_has_no_instance_block_and_empty_collections()
    {
        var connection = SqlFixtures.Connection(SqlEngines.MySql);
        var snapshot = SqlSnapshotAssembler.Unavailable(connection, Guid.NewGuid(), SqlFixtures.CollectedAt, "1.2.0", SqlUnavailableReasons.AuthenticationFailed);

        Assert.Null(snapshot.Instance);
        Assert.Empty(snapshot.Databases);
        Assert.Empty(snapshot.Accounts);
        Assert.Equal([new SqlUnavailableSection(SqlSections.Instance, SqlUnavailableReasons.AuthenticationFailed)], snapshot.Unavailable);
        Assert.Equal(connection, snapshot.Connection);
    }

    private static SqlAuditSnapshot Assemble(CatalogSection server, IReadOnlyList<CatalogDatabase>? databases = null) =>
        SqlFixtures.Snapshot(SqlEngines.SqlServer, new SqlCatalog(Instance, databases ?? [], [server], []));

    private static CatalogSection Server(
        IReadOnlyList<CatalogAccount> accounts, IReadOnlyList<CatalogMembership>? memberships = null, IReadOnlyList<CatalogPermission>? permissions = null) =>
        new(SqlSections.Server, accounts, memberships ?? [], permissions ?? [], 0);

    private static CatalogAccount Account(
        string key, string name, string level = SqlAccountLevels.Server, string? database = null, string type = SqlAccountTypes.SqlLogin, string? loginKey = null) =>
        new(key, level, database, name, null, type, true, false, null, null, null, null, loginKey, false, null);
}

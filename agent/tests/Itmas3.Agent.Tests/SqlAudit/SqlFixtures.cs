using System.Text.Json;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.Model;

namespace Itmas3.Agent.Tests.SqlAudit;

/// <summary>Row fixtures of the catalog queries (fixtures/sqlaudit/*.rows.json) and helpers to inspect snapshots.</summary>
internal static class SqlFixtures
{
    public static readonly DateTimeOffset CollectedAt = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static T Rows<T>(string file) =>
        JsonSerializer.Deserialize<T>(Fixtures.Read($"sqlaudit/{file}"), Options) ?? throw new InvalidOperationException(file);

    public static SqlConnectionInfo Connection(string engine) =>
        new("prueba", engine, "db.example.test", SqlConnectionSettings.DefaultPort(engine), null, SqlTlsModes.Verify);

    public static SqlAuditSnapshot Snapshot(string engine, SqlCatalog catalog) =>
        SqlSnapshotAssembler.Assemble(Connection(engine), catalog, Guid.Parse("6f1c2b9e-4d3a-4f6b-9a8e-2c1d0e5f7a90"), CollectedAt, "1.2.0");

    public static SqlAccountInfo Account(this SqlAuditSnapshot snapshot, string name, string? database = null, string? host = null) =>
        Assert.Single(snapshot.Accounts, account => account.Name == name && account.Database == database && (host is null || account.Host == host));

    public static List<SqlPermissionInfo> PermissionsOf(this SqlAuditSnapshot snapshot, SqlAccountInfo account) =>
        snapshot.Permissions.Where(permission => permission.GranteeRef == account.Ref).ToList();

    public static List<string> RolesOf(this SqlAuditSnapshot snapshot, SqlAccountInfo account) =>
        snapshot.RoleMemberships.Where(membership => membership.MemberRef == account.Ref)
            .Select(membership => snapshot.Accounts.Single(role => role.Ref == membership.RoleRef).Name)
            .ToList();

    public static string NameOf(this SqlAuditSnapshot snapshot, string? reference) =>
        snapshot.Accounts.Single(account => account.Ref == reference).Name;
}

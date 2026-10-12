using Itmas3.Domain.Common;
using Itmas3.Domain.SqlAudit;

namespace Itmas3.UnitTests.Domain.SqlAudit;

/// <summary>SPEC-0005 RN-2..RN-6 and RF-17 on accounts and elements.</summary>
public sealed class SqlAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly SqlValidationActor Analyst = new(Guid.NewGuid(), "Analista");

    private static readonly SqlAccountAttributes Attributes = new(
        true, false, new DateTimeOffset(2025, 3, 1, 8, 0, 0, TimeSpan.Zero), Now, "Ventas", null, null, false,
        new SqlPasswordAttributes(true, true, false, null));

    [Fact]
    public void A_validated_account_whose_attributes_change_needs_revalidation()
    {
        var account = Discover();
        Assert.True(account.Validate(Analyst, Now));
        Assert.False(account.Validate(Analyst, Now));

        var unchanged = account.Observe(Attributes with { ModifiedAt = Now.AddDays(1) }, Now.AddDays(1));
        Assert.Empty(unchanged.Changes);
        Assert.Equal(SqlValidationStatus.Valid, account.ValidationStatus);

        var observation = account.Observe(Attributes with { Enabled = false }, Now.AddDays(2));

        Assert.Equal(SqlValidationStatus.Changed, account.ValidationStatus);
        var change = Assert.Single(observation.Changes);
        Assert.Equal(new SqlFieldChange("enabled", "true", "false"), change);
        Assert.True(account.Validate(Analyst, Now.AddDays(2)));
        Assert.Equal(account.Fingerprint, account.ValidatedFingerprint);
    }

    [Fact]
    public void An_account_created_again_with_the_same_name_changes_its_fingerprint()
    {
        var account = Discover();
        var fingerprint = account.Fingerprint;

        account.Observe(Attributes with { CreatedAt = Now }, Now);

        Assert.NotEqual(fingerprint, account.Fingerprint);
        Assert.Contains(account.Observe(Attributes, Now).Changes, change => change.Field == "createdAt");
    }

    [Fact]
    public void Removed_accounts_cannot_be_validated_and_come_back_pending()
    {
        var account = Discover();
        account.Validate(Analyst, Now);
        Assert.True(account.MarkRemoved(Now));
        Assert.False(account.MarkRemoved(Now));

        Assert.Throws<DomainException>(() => account.Validate(Analyst, Now));

        var observation = account.Observe(Attributes, Now.AddDays(1));
        Assert.True(observation.Reappeared);
        Assert.Null(account.RemovedAt);
        Assert.Equal(SqlValidationStatus.Pending, account.ValidationStatus);
        Assert.Null(account.ValidatedBy);
    }

    [Fact]
    public void Unvalidate_is_idempotent()
    {
        var account = Discover();
        Assert.False(account.Unvalidate());
        account.Validate(Analyst, Now);
        Assert.True(account.Unvalidate());
        Assert.Equal(SqlValidationStatus.Pending, account.ValidationStatus);
    }

    [Fact]
    public void Identity_distinguishes_level_database_host_and_type()
    {
        var server = SqlIdentity.Account(SqlAccountLevel.Server, null, "app", null, "sql-login");

        Assert.NotEqual(server, SqlIdentity.Account(SqlAccountLevel.Database, "Ventas", "app", null, "database-user"));
        Assert.NotEqual(SqlIdentity.Account(SqlAccountLevel.Server, null, "app", "%", "mysql-account"), SqlIdentity.Account(SqlAccountLevel.Server, null, "app", "localhost", "mysql-account"));
        Assert.NotEqual(SqlIdentity.Account(SqlAccountLevel.Server, null, "a", "b", "other"), SqlIdentity.Account(SqlAccountLevel.Server, null, "a\u001Fb", null, "other"));
        Assert.Equal(server, SqlIdentity.Account(SqlAccountLevel.Server, null, "app", null, "sql-login"));
    }

    [Fact]
    public void Elements_are_validated_on_their_own_and_removed_ones_cannot_be_marked()
    {
        var instanceId = Guid.NewGuid();
        var account = SqlAccount.Discover(instanceId, SqlAccountLevel.Server, null, "app", null, "sql-login", Attributes, Now);
        var item = SqlAccessItem.Grant(
            instanceId, account, new SqlSecurable(SqlSecurableClass.Object, "Ventas", "dbo", "Facturas", "table", null), "DELETE", SqlPermissionState.Grant, "dbo", RiskLevel.High, Now);
        var other = SqlAccessItem.Grant(
            instanceId, account, new SqlSecurable(SqlSecurableClass.Object, "Ventas", "dbo", "Facturas", "table", null), "DELETE", SqlPermissionState.GrantWithGrantOption, "dbo", RiskLevel.High, Now);

        Assert.NotEqual(item.IdentityHash, other.IdentityHash);
        Assert.True(item.Validate(Analyst, Now));
        Assert.False(item.Validate(Analyst, Now));
        Assert.Equal("Analista", item.ValidatedBy);
        Assert.True(item.Unvalidate());
        Assert.Throws<DomainException>(() => SqlAccessItem.Grant(Guid.NewGuid(), account, item.Securable!, "SELECT", SqlPermissionState.Grant, null, RiskLevel.General, Now));
    }

    [Fact]
    public void Instance_keys_are_normalised_and_unavailable_sections_cover_their_elements()
    {
        var key = SqlInstanceKey.Create(SqlEngine.SqlServer, " SQL01.Example.Internal. ", 1433, "prod");
        Assert.Equal("sql01.example.internal", key.Host);
        Assert.Equal("PROD", key.InstanceName);
        Assert.Throws<DomainException>(() => SqlInstanceKey.Create(SqlEngine.MySql, "db", 70000, null));

        IReadOnlyList<SqlUnavailablePart> unavailable = [new("database:Ventas", "offline")];
        Assert.False(SqlScopes.WasRead(unavailable, "Ventas"));
        Assert.True(SqlScopes.WasRead(unavailable, "Compras"));
        Assert.True(SqlScopes.WasRead(unavailable, null));
        Assert.False(SqlScopes.WasRead([new("instance", "timeout")], null));
        Assert.False(SqlScopes.WasRead([new("server", "permission-denied")], null));
    }

    private static SqlAccount Discover() =>
        SqlAccount.Discover(Guid.NewGuid(), SqlAccountLevel.Server, null, "app_ventas", null, "sql-login", Attributes, Now);
}

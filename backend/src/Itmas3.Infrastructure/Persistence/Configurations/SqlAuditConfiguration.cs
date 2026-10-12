using System.Text.Json;
using Itmas3.Application.SqlAudit;
using Itmas3.Domain.Inventory;
using Itmas3.Domain.SqlAudit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Itmas3.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persistence of the SQL access audit (SPEC-0005 §13). Enumerations are stored with their wire codes, risks as
/// <c>smallint</c> so they sort by severity, and the small per-instance lists and change details as <c>jsonb</c>.
/// </summary>
internal sealed class SqlInstanceConfiguration : IEntityTypeConfiguration<SqlInstance>
{
    public void Configure(EntityTypeBuilder<SqlInstance> builder)
    {
        builder.ToTable("sql_instances");
        builder.HasKey(instance => instance.Id);
        builder.Property(instance => instance.Id).ValueGeneratedNever();
        builder.Property(instance => instance.Engine).HasConversion(SqlAuditConverters.Engine).HasMaxLength(16).IsRequired();
        builder.Property(instance => instance.Host).HasMaxLength(SqlInstance.MaxTextLength).IsRequired();
        builder.Property(instance => instance.InstanceName).HasMaxLength(SqlInstance.MaxInstanceNameLength);
        builder.Property(instance => instance.Alias).HasMaxLength(SqlInstance.MaxAliasLength).IsRequired();
        builder.Property(instance => instance.TlsMode).HasConversion(SqlAuditConverters.TlsMode).HasMaxLength(16).IsRequired();
        builder.Property(instance => instance.Version).HasMaxLength(SqlInstance.MaxTextLength);
        builder.Property(instance => instance.Edition).HasMaxLength(SqlInstance.MaxTextLength);
        builder.Property(instance => instance.AuthenticationMode).HasMaxLength(16);
        builder.Property(instance => instance.Databases)
            .HasConversion(SqlAuditConverters.Json<IReadOnlyList<SqlDatabaseInfo>>(), SqlAuditConverters.JsonComparer<IReadOnlyList<SqlDatabaseInfo>>())
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(instance => instance.Unavailable)
            .HasConversion(SqlAuditConverters.Json<IReadOnlyList<SqlUnavailablePart>>(), SqlAuditConverters.JsonComparer<IReadOnlyList<SqlUnavailablePart>>())
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(instance => instance.OmittedBuiltinPermissions).HasColumnName("omitted_builtin_permissions");
        // Concurrent snapshots of the same instance: the second one fails and the agent retries (xmin).
        builder.Property<uint>("RowVersion").IsRowVersion();

        // RN-1 / P-23. NULLS NOT DISTINCT: a missing port or instance name still identifies one instance.
        builder.HasIndex(instance => new { instance.DeviceId, instance.Engine, instance.Host, instance.Port, instance.InstanceName })
            .IsUnique()
            .AreNullsDistinct(false);
        builder.HasIndex(instance => instance.Alias);
        builder.HasOne<Device>().WithMany().HasForeignKey(instance => instance.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SqlSnapshotReceiptConfiguration : IEntityTypeConfiguration<SqlSnapshotReceipt>
{
    public void Configure(EntityTypeBuilder<SqlSnapshotReceipt> builder)
    {
        builder.ToTable("sql_snapshot_receipts");
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id).ValueGeneratedNever();
        builder.Property(receipt => receipt.ConnectionIp).HasMaxLength(Device.MaxIpLength);
        builder.HasIndex(receipt => new { receipt.DeviceId, receipt.SubmissionId }).IsUnique();
        builder.HasIndex(receipt => receipt.ReceivedAt);
        builder.HasIndex(receipt => receipt.InstanceId);
        builder.HasOne<Device>().WithMany().HasForeignKey(receipt => receipt.DeviceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SqlInstance>().WithMany().HasForeignKey(receipt => receipt.InstanceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SqlAccountConfiguration : IEntityTypeConfiguration<SqlAccount>
{
    public void Configure(EntityTypeBuilder<SqlAccount> builder)
    {
        builder.ToTable("sql_accounts");
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).ValueGeneratedNever();
        builder.Property(account => account.IdentityHash).HasMaxLength(SqlIdentity.HashLength).IsFixedLength().IsRequired();
        builder.Property(account => account.Level).HasConversion(SqlAuditConverters.Level).HasMaxLength(16).IsRequired();
        builder.Property(account => account.DatabaseName).HasMaxLength(SqlAccount.MaxTextLength);
        builder.Property(account => account.Name).HasMaxLength(SqlAccount.MaxTextLength).IsRequired();
        builder.Property(account => account.Host).HasMaxLength(SqlAccount.MaxTextLength);
        builder.Property(account => account.AccountType).HasMaxLength(SqlAccount.MaxTypeLength).IsRequired();
        builder.Property(account => account.DefaultDatabase).HasMaxLength(SqlAccount.MaxTextLength);
        builder.Property(account => account.DefaultSchema).HasMaxLength(SqlAccount.MaxTextLength);
        builder.Property(account => account.LoginName).HasMaxLength(SqlAccount.MaxTextLength);
        builder.Property(account => account.Fingerprint).HasMaxLength(SqlIdentity.HashLength).IsFixedLength().IsRequired();
        builder.Property(account => account.OwnRisk).HasColumnName("risk_level").HasConversion<short>();
        builder.Property(account => account.InheritedRisk).HasColumnName("inherited_risk_level").HasConversion<short>();
        builder.Property(account => account.ValidationStatus).HasConversion(SqlAuditConverters.ValidationStatus).HasMaxLength(16).IsRequired();
        builder.Property(account => account.ValidatedFingerprint).HasMaxLength(SqlIdentity.HashLength).IsFixedLength();
        builder.Property(account => account.ValidatedBy).HasMaxLength(SqlAccount.MaxActorLength);
        builder.Ignore(account => account.Attributes);
        builder.Ignore(account => account.EffectiveRisk);
        builder.Property<uint>("RowVersion").IsRowVersion();

        builder.HasIndex(account => new { account.InstanceId, account.IdentityHash }).IsUnique();
        builder.HasIndex(account => new { account.InstanceId, account.Name });
        builder.HasIndex(account => account.RemovedAt);
        builder.HasOne<SqlInstance>().WithMany().HasForeignKey(account => account.InstanceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SqlAccessItemConfiguration : IEntityTypeConfiguration<SqlAccessItem>
{
    /// <summary>Partial indexes over the current elements: the ones the screens count and filter (RF-18, RF-23).</summary>
    public const string CurrentFilter = "removed_at IS NULL";

    public void Configure(EntityTypeBuilder<SqlAccessItem> builder)
    {
        builder.ToTable("sql_access_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.IdentityHash).HasMaxLength(SqlIdentity.HashLength).IsFixedLength().IsRequired();
        builder.Property(item => item.Kind).HasConversion(SqlAuditConverters.Kind).HasMaxLength(16).IsRequired();
        builder.Property(item => item.SecurableClass).HasConversion(SqlAuditConverters.SecurableClass).HasMaxLength(20);
        builder.Property(item => item.DatabaseName).HasMaxLength(SqlAccessItem.MaxTextLength);
        builder.Property(item => item.SchemaName).HasMaxLength(SqlAccessItem.MaxTextLength);
        builder.Property(item => item.ObjectName).HasMaxLength(SqlAccessItem.MaxTextLength);
        builder.Property(item => item.ObjectType).HasMaxLength(32);
        builder.Property(item => item.ColumnName).HasMaxLength(SqlAccessItem.MaxTextLength);
        builder.Property(item => item.Permission).HasMaxLength(SqlAccessItem.MaxPermissionLength);
        builder.Property(item => item.State).HasConversion(SqlAuditConverters.State).HasMaxLength(24);
        builder.Property(item => item.Grantor).HasMaxLength(SqlAccessItem.MaxTextLength);
        builder.Property(item => item.Risk).HasColumnName("risk_level").HasConversion<short>();
        builder.Property(item => item.ValidationStatus).HasConversion(SqlAuditConverters.ValidationStatus).HasMaxLength(16).IsRequired();
        builder.Property(item => item.ValidatedBy).HasMaxLength(SqlAccount.MaxActorLength);
        builder.Ignore(item => item.Securable);
        // No row version: inserting hundreds of thousands of elements must not read a value back per row (RNF-2).
        // Marking and unmarking are idempotent, so concurrent clicks on the same element need no conflict detection.

        builder.HasIndex(item => new { item.AccountId, item.IdentityHash }).IsUnique();
        // Permissions view of an instance (RF-23) and pending counters (RF-18, RF-20).
        builder.HasIndex(item => new { item.InstanceId, item.Risk, item.ValidationStatus }).HasFilter(CurrentFilter);
        builder.HasIndex(item => new { item.AccountId, item.ValidationStatus }).HasFilter(CurrentFilter);
        builder.HasIndex(item => item.RoleAccountId);
        builder.HasIndex(item => item.RemovedAt);
        builder.HasOne<SqlInstance>().WithMany().HasForeignKey(item => item.InstanceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SqlAccount>().WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SqlAccount>().WithMany().HasForeignKey(item => item.RoleAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SqlAccessChangeConfiguration : IEntityTypeConfiguration<SqlAccessChange>
{
    public void Configure(EntityTypeBuilder<SqlAccessChange> builder)
    {
        builder.ToTable("sql_access_changes");
        builder.HasKey(change => change.Id);
        builder.Property(change => change.Id).ValueGeneratedNever();
        builder.Property(change => change.ChangeType).HasConversion(SqlAuditConverters.ChangeType).HasMaxLength(16).IsRequired();
        builder.Property(change => change.Target).HasConversion(SqlAuditConverters.ChangeTarget).HasMaxLength(20).IsRequired();
        builder.Property(change => change.Details)
            .HasConversion(SqlAuditConverters.Json<SqlChangeDetails>(), SqlAuditConverters.JsonComparer<SqlChangeDetails>())
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(change => new { change.AccountId, change.DetectedAt }).IsDescending(false, true);
        builder.HasIndex(change => change.DetectedAt);
        builder.HasIndex(change => change.InstanceId);
        builder.HasOne<SqlInstance>().WithMany().HasForeignKey(change => change.InstanceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SqlAccount>().WithMany().HasForeignKey(change => change.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SqlAccessItem>().WithMany().HasForeignKey(change => change.ItemId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal static class SqlAuditConverters
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    public static readonly ValueConverter<SqlEngine, string> Engine =
        new(value => SqlAuditCodes.ToCode(value), code => SqlAuditCodes.ParseEngine(code) ?? SqlEngine.SqlServer);

    public static readonly ValueConverter<SqlTlsMode, string> TlsMode =
        new(value => SqlAuditCodes.ToCode(value), code => SqlAuditCodes.ParseTlsMode(code) ?? SqlTlsMode.Verify);

    public static readonly ValueConverter<SqlAccountLevel, string> Level =
        new(value => SqlAuditCodes.ToCode(value), code => SqlAuditCodes.ParseLevel(code) ?? SqlAccountLevel.Server);

    public static readonly ValueConverter<SqlValidationStatus, string> ValidationStatus =
        new(value => SqlAuditCodes.ToCode(value), code => SqlAuditCodes.ParseValidationStatus(code));

    public static readonly ValueConverter<SqlAccessItemKind, string> Kind =
        new(value => SqlAuditCodes.ToCode(value), code => SqlAuditCodes.ParseKind(code) ?? SqlAccessItemKind.Permission);

    public static readonly ValueConverter<SqlSecurableClass, string> SecurableClass =
        new(value => SqlAuditCodes.ToCode(value), code => SqlAuditCodes.ParseSecurableClass(code) ?? SqlSecurableClass.Server);

    public static readonly ValueConverter<SqlPermissionState, string> State =
        new(value => SqlAuditCodes.ToCode(value), code => SqlAuditCodes.ParseState(code) ?? SqlPermissionState.Grant);

    public static readonly ValueConverter<SqlChangeType, string> ChangeType =
        new(value => SqlAuditCodes.ToCode(value), code => SqlAuditCodes.ParseChangeType(code));

    public static readonly ValueConverter<SqlChangeTarget, string> ChangeTarget =
        new(value => SqlAuditCodes.ToCode(value), code => SqlAuditCodes.ParseChangeTarget(code));

    public static ValueConverter<T, string> Json<T>() =>
        new(value => JsonSerializer.Serialize(value, JsonOptions), json => JsonSerializer.Deserialize<T>(json, JsonOptions)!);

    public static ValueComparer<T> JsonComparer<T>() => new(
        (left, right) => JsonSerializer.Serialize(left, JsonOptions) == JsonSerializer.Serialize(right, JsonOptions),
        value => JsonSerializer.Serialize(value, JsonOptions).GetHashCode(StringComparison.Ordinal),
        value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!);
}

using System.Globalization;
using Itmas3.Domain.Common;

namespace Itmas3.Domain.SqlAudit;

/// <summary>
/// An account of an audited instance (SPEC-0005 RF-4): a login, user or role of the engine, at server level or (SQL
/// Server only) of one database. Identified by <see cref="IdentityHash"/> (RN-2). Its validation refers to the
/// attributes it had when it was validated (<see cref="ValidatedFingerprint"/>, RN-4).
/// </summary>
public sealed class SqlAccount : Entity<Guid>
{
    public const int MaxTextLength = 256;
    public const int MaxTypeLength = 32;
    public const int MaxActorLength = 200;

    private SqlAccount(
        Guid id, Guid instanceId, string identityHash, SqlAccountLevel level, string? databaseName, string name, string? host, string accountType)
        : base(id)
    {
        InstanceId = instanceId;
        IdentityHash = identityHash;
        Level = level;
        DatabaseName = databaseName;
        Name = name;
        Host = host;
        AccountType = accountType;
    }

    public Guid InstanceId { get; private set; }

    public string IdentityHash { get; private set; }

    public SqlAccountLevel Level { get; private set; }

    public string? DatabaseName { get; private set; }

    public string Name { get; private set; }

    /// <summary>Host part of a MySQL or MariaDB account (<c>user@host</c>); null for other engines.</summary>
    public string? Host { get; private set; }

    public string AccountType { get; private set; }

    public bool Enabled { get; private set; }

    public bool Locked { get; private set; }

    public DateTimeOffset? EngineCreatedAt { get; private set; }

    public DateTimeOffset? EngineModifiedAt { get; private set; }

    public string? DefaultDatabase { get; private set; }

    public string? DefaultSchema { get; private set; }

    /// <summary>Login a SQL Server database user is mapped to.</summary>
    public string? LoginName { get; private set; }

    public bool IsOrphaned { get; private set; }

    /// <summary>Whether the engine reports password attributes for this account at all.</summary>
    public bool HasPassword { get; private set; }

    public bool? PasswordPolicyEnforced { get; private set; }

    public bool? PasswordExpirationEnforced { get; private set; }

    public bool? PasswordExpired { get; private set; }

    public DateTimeOffset? PasswordValidUntil { get; private set; }

    /// <summary>Hash of the attributes (RN-4).</summary>
    public string Fingerprint { get; private set; } = string.Empty;

    /// <summary>Risk of the account itself and of its direct roles and permissions (RF-14).</summary>
    public RiskLevel OwnRisk { get; private set; } = RiskLevel.General;

    /// <summary>Risk inherited from the roles it is a member of, directly or not (P-24).</summary>
    public RiskLevel InheritedRisk { get; private set; } = RiskLevel.General;

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public SqlValidationStatus ValidationStatus { get; private set; }

    public string? ValidatedFingerprint { get; private set; }

    public Guid? ValidatedByUserId { get; private set; }

    public string? ValidatedBy { get; private set; }

    public DateTimeOffset? ValidatedAt { get; private set; }

    public RiskLevel EffectiveRisk => InheritedRisk > OwnRisk ? InheritedRisk : OwnRisk;

    public SqlAccountAttributes Attributes => new(
        Enabled,
        Locked,
        EngineCreatedAt,
        EngineModifiedAt,
        DefaultDatabase,
        DefaultSchema,
        LoginName,
        IsOrphaned,
        HasPassword ? new SqlPasswordAttributes(PasswordPolicyEnforced, PasswordExpirationEnforced, PasswordExpired, PasswordValidUntil) : null);

    /// <summary>An account seen for the first time: pending validation (RF-17).</summary>
    public static SqlAccount Discover(
        Guid instanceId,
        SqlAccountLevel level,
        string? databaseName,
        string name,
        string? host,
        string accountType,
        SqlAccountAttributes attributes,
        DateTimeOffset seenAt)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        if (level == SqlAccountLevel.Database ? string.IsNullOrEmpty(databaseName) : databaseName is not null)
        {
            throw new DomainException("La base de datos de la cuenta no corresponde a su nivel.");
        }

        // Only a MySQL/MariaDB anonymous account (''@host) has an empty name.
        if (name is null || string.IsNullOrEmpty(accountType) || (name.Length == 0 && accountType != "mysql-account"))
        {
            throw new DomainException("La cuenta debe tener nombre y tipo.");
        }

        var account = new SqlAccount(
            Guid.CreateVersion7(seenAt),
            instanceId,
            SqlIdentity.Account(level, databaseName, name, host, accountType),
            level,
            databaseName,
            name,
            host,
            accountType)
        {
            FirstSeenAt = seenAt,
            LastSeenAt = seenAt,
            ValidationStatus = SqlValidationStatus.Pending,
        };
        account.SetAttributes(attributes);
        return account;
    }

    /// <summary>
    /// The account is present in a new snapshot. Returns the attribute changes (empty when nothing changed). A validated
    /// account whose attributes no longer match the validated fingerprint becomes "changed" (RN-4); an account that had
    /// been removed and appears again is a new element, pending validation.
    /// </summary>
    public SqlAccountObservation Observe(SqlAccountAttributes attributes, DateTimeOffset seenAt)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        var reappeared = RemovedAt is not null;
        var changes = reappeared ? [] : Differences(Attributes, attributes);
        var fingerprintChanged = SqlIdentity.Fingerprint(AccountType, attributes) != Fingerprint;

        SetAttributes(attributes);
        LastSeenAt = seenAt;
        if (reappeared)
        {
            RemovedAt = null;
            FirstSeenAt = seenAt;
            ClearValidation();
        }
        else if (ValidationStatus == SqlValidationStatus.Valid && Fingerprint != ValidatedFingerprint)
        {
            ValidationStatus = SqlValidationStatus.Changed;
        }

        return new SqlAccountObservation(reappeared, fingerprintChanged ? changes : []);
    }

    /// <summary>The account is no longer in the engine (RF-11). It keeps its validation, but it can no longer be marked.</summary>
    public bool MarkRemoved(DateTimeOffset at)
    {
        if (RemovedAt is not null)
        {
            return false;
        }

        RemovedAt = at;
        return true;
    }

    public void SetRisk(RiskLevel own, RiskLevel inherited)
    {
        OwnRisk = own == RiskLevel.Denied ? RiskLevel.General : own;
        InheritedRisk = inherited == RiskLevel.Denied ? RiskLevel.General : inherited;
    }

    /// <summary>
    /// Marks the account as valid with the fingerprint the user saw (RF-16, RN-6). The caller checks that the
    /// fingerprint is the current one. Returns false when it was already valid with that fingerprint (idempotent).
    /// </summary>
    public bool Validate(SqlValidationActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (RemovedAt is not null)
        {
            throw new DomainException("La cuenta ya no figura en el motor y no se puede marcar como válida.");
        }

        if (ValidationStatus == SqlValidationStatus.Valid && ValidatedFingerprint == Fingerprint)
        {
            return false;
        }

        ValidationStatus = SqlValidationStatus.Valid;
        ValidatedFingerprint = Fingerprint;
        ValidatedByUserId = actor.UserId;
        ValidatedBy = Truncate(actor.DisplayName, MaxActorLength);
        ValidatedAt = now;
        return true;
    }

    /// <summary>Back to pending (RF-16). Idempotent: returns false when it was already pending.</summary>
    public bool Unvalidate()
    {
        if (ValidationStatus == SqlValidationStatus.Pending)
        {
            return false;
        }

        ClearValidation();
        return true;
    }

    private void ClearValidation()
    {
        ValidationStatus = SqlValidationStatus.Pending;
        ValidatedFingerprint = null;
        ValidatedByUserId = null;
        ValidatedBy = null;
        ValidatedAt = null;
    }

    private void SetAttributes(SqlAccountAttributes attributes)
    {
        Enabled = attributes.Enabled;
        Locked = attributes.Locked;
        EngineCreatedAt = attributes.CreatedAt;
        EngineModifiedAt = attributes.ModifiedAt;
        DefaultDatabase = attributes.DefaultDatabase;
        DefaultSchema = attributes.DefaultSchema;
        LoginName = attributes.Login;
        IsOrphaned = attributes.IsOrphaned;
        HasPassword = attributes.Password is not null;
        PasswordPolicyEnforced = attributes.Password?.PolicyEnforced;
        PasswordExpirationEnforced = attributes.Password?.ExpirationEnforced;
        PasswordExpired = attributes.Password?.Expired;
        PasswordValidUntil = attributes.Password?.ValidUntil;
        Fingerprint = SqlIdentity.Fingerprint(AccountType, attributes);
    }

    /// <summary>Readable differences between two sets of attributes, for the change log.</summary>
    public static IReadOnlyList<SqlFieldChange> Differences(SqlAccountAttributes before, SqlAccountAttributes after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var changes = new List<SqlFieldChange>();
        Compare(changes, "enabled", Text(before.Enabled), Text(after.Enabled));
        Compare(changes, "locked", Text(before.Locked), Text(after.Locked));
        Compare(changes, "createdAt", Text(before.CreatedAt), Text(after.CreatedAt));
        Compare(changes, "defaultDatabase", before.DefaultDatabase, after.DefaultDatabase);
        Compare(changes, "defaultSchema", before.DefaultSchema, after.DefaultSchema);
        Compare(changes, "login", before.Login, after.Login);
        Compare(changes, "isOrphaned", Text(before.IsOrphaned), Text(after.IsOrphaned));
        Compare(changes, "password.policyEnforced", Text(before.Password?.PolicyEnforced), Text(after.Password?.PolicyEnforced));
        Compare(changes, "password.expirationEnforced", Text(before.Password?.ExpirationEnforced), Text(after.Password?.ExpirationEnforced));
        Compare(changes, "password.expired", Text(before.Password?.Expired), Text(after.Password?.Expired));
        Compare(changes, "password.validUntil", Text(before.Password?.ValidUntil), Text(after.Password?.ValidUntil));
        return changes;
    }

    private static void Compare(List<SqlFieldChange> changes, string field, string? before, string? after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            changes.Add(new SqlFieldChange(field, before, after));
        }
    }

    private static string? Text(bool? value) => value switch
    {
        true => "true",
        false => "false",
        null => null,
    };

    // Second precision: the database keeps microseconds, the engines report milliseconds at most.
    private static string? Text(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}

/// <summary>Result of seeing an existing account again in a snapshot.</summary>
public sealed record SqlAccountObservation(bool Reappeared, IReadOnlyList<SqlFieldChange> Changes);

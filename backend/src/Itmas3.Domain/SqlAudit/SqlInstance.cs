using Itmas3.Domain.Common;

namespace Itmas3.Domain.SqlAudit;

/// <summary>
/// A database engine instance audited by the agent of a device (SPEC-0005 RN-1). It belongs to the device whose API key
/// sent its snapshots and is identified by engine + normalised host + port + instance name (P-23). It keeps the data of
/// the latest applied snapshot; accounts and elements live in their own tables.
/// </summary>
public sealed class SqlInstance : Entity<Guid>
{
    public const int MaxAliasLength = 64;
    public const int MaxTextLength = 256;
    public const int MaxInstanceNameLength = 128;

    private SqlInstance(Guid id, Guid deviceId, SqlEngine engine, string host, int? port, string? instanceName, DateTimeOffset createdAt)
        : base(id)
    {
        DeviceId = deviceId;
        Engine = engine;
        Host = host;
        Port = port;
        InstanceName = instanceName;
        CreatedAt = createdAt;
    }

    public Guid DeviceId { get; private set; }

    public SqlEngine Engine { get; private set; }

    public string Host { get; private set; }

    public int? Port { get; private set; }

    public string? InstanceName { get; private set; }

    /// <summary>Name given to the connection by the device administrator; it may change between snapshots.</summary>
    public string Alias { get; private set; } = string.Empty;

    public SqlTlsMode TlsMode { get; private set; }

    public string? Version { get; private set; }

    public string? Edition { get; private set; }

    public string? AuthenticationMode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Collection date of the latest applied snapshot (RF-12).</summary>
    public DateTimeOffset? LastCollectedAt { get; private set; }

    public DateTimeOffset? LastReceivedAt { get; private set; }

    public IReadOnlyList<SqlDatabaseInfo> Databases { get; private set; } = [];

    /// <summary>Parts that could not be read in the latest applied snapshot (RF-7, RF-13).</summary>
    public IReadOnlyList<SqlUnavailablePart> Unavailable { get; private set; } = [];

    /// <summary>Built-in default permissions granted to <c>public</c> that the agent left out (RF-5).</summary>
    public int OmittedBuiltinPermissions { get; private set; }

    /// <summary>Version of <see cref="SqlRiskCatalog"/> the stored risks were calculated with (RF-15).</summary>
    public int RiskCatalogVersion { get; private set; }

    public static SqlInstance Register(Guid deviceId, SqlInstanceKey key, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new SqlInstance(Guid.CreateVersion7(now), deviceId, key.Engine, key.Host, key.Port, key.InstanceName, now);
    }

    /// <summary>Whether a snapshot collected at <paramref name="collectedAt"/> is newer than the current state (RF-12).</summary>
    public bool Accepts(DateTimeOffset collectedAt) => LastCollectedAt is not { } last || collectedAt > last;

    /// <summary>
    /// Replaces the instance data with the snapshot header. When the connection itself failed (<paramref name="metadata"/>
    /// is null) the previous version and edition are kept: unavailable data is never replaced by guesses (RN-7).
    /// </summary>
    public void ApplySnapshot(
        string alias,
        SqlTlsMode tlsMode,
        SqlInstanceMetadata? metadata,
        IReadOnlyList<SqlDatabaseInfo> databases,
        IReadOnlyList<SqlUnavailablePart> unavailable,
        int omittedBuiltinPermissions,
        DateTimeOffset collectedAt,
        DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(databases);
        ArgumentNullException.ThrowIfNull(unavailable);
        if (!Accepts(collectedAt))
        {
            throw new DomainException("La instantánea es más antigua que el estado actual de la instancia.");
        }

        if (string.IsNullOrWhiteSpace(alias) || alias.Length > MaxAliasLength || omittedBuiltinPermissions < 0)
        {
            throw new DomainException("La instantánea no tiene un alias o un recuento de permisos omitidos válido.");
        }

        Alias = alias;
        TlsMode = tlsMode;
        if (metadata is not null)
        {
            Version = metadata.Version;
            Edition = metadata.Edition;
            AuthenticationMode = metadata.AuthenticationMode;
            Databases = databases;
        }

        Unavailable = unavailable;
        OmittedBuiltinPermissions = omittedBuiltinPermissions;
        LastCollectedAt = collectedAt;
        LastReceivedAt = receivedAt;
    }

    /// <summary>Records the arrival of a snapshot that is older than the current state (RF-12).</summary>
    public void RecordLateSnapshot(DateTimeOffset receivedAt) => LastReceivedAt = receivedAt;

    public void MarkRiskCalculated(int catalogVersion) => RiskCatalogVersion = catalogVersion;

    /// <summary>Whether the elements of <paramref name="database"/> (null: the server level) were read in the latest snapshot.</summary>
    public bool WasRead(string? database) => SqlScopes.WasRead(Unavailable, database);
}

/// <summary>Header data of a snapshot whose connection succeeded.</summary>
public sealed record SqlInstanceMetadata(string Version, string? Edition, string AuthenticationMode);

/// <summary>
/// Sections of a snapshot (contract §1): an account or element belongs to the server level (no database) or to one
/// database. A section listed as unavailable keeps the previous state of its elements (RF-13).
/// </summary>
public static class SqlScopes
{
    public static bool WasRead(IReadOnlyList<SqlUnavailablePart> unavailable, string? database)
    {
        ArgumentNullException.ThrowIfNull(unavailable);
        foreach (var part in unavailable)
        {
            if (part.Section == SqlUnavailablePart.InstanceSection
                || (database is null && part.Section == SqlUnavailablePart.ServerSection)
                || (database is not null && part.Section == SqlUnavailablePart.DatabaseSection(database)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The unavailable part that covers <paramref name="database"/>, or null when it was read.</summary>
    public static SqlUnavailablePart? Covering(IReadOnlyList<SqlUnavailablePart> unavailable, string? database)
    {
        ArgumentNullException.ThrowIfNull(unavailable);
        return unavailable.FirstOrDefault(part => !WasRead([part], database));
    }
}

namespace Itmas3.Agent.Collectors.SqlAudit.Model;

// Contract sql-audit v1 (SPEC-0005 §12.2). Every property is always serialized (nulls included): the API is strict and
// rejects unknown properties. Keep in sync with agent/tests/fixtures/contract/sql-audit.v1.json (SqlAuditContractTests).

public sealed record SqlAuditSnapshot(
    int SchemaVersion,
    Guid SubmissionId,
    DateTimeOffset CollectedAt,
    string AgentVersion,
    SqlConnectionInfo Connection,
    SqlInstanceInfo? Instance,
    IReadOnlyList<SqlDatabaseInfo> Databases,
    IReadOnlyList<SqlAccountInfo> Accounts,
    IReadOnlyList<SqlRoleMembershipInfo> RoleMemberships,
    IReadOnlyList<SqlPermissionInfo> Permissions,
    SqlOmittedInfo Omitted,
    IReadOnlyList<SqlUnavailableSection> Unavailable)
{
    public const int CurrentSchemaVersion = 1;
}

/// <summary>Where the agent connected. Never contains the account or its password.</summary>
public sealed record SqlConnectionInfo(string Alias, string Engine, string Host, int? Port, string? InstanceName, string Tls);

public sealed record SqlInstanceInfo(string Version, string? Edition, string AuthenticationMode);

public sealed record SqlDatabaseInfo(string Name, string State, string? OwnerRef);

public sealed record SqlAccountInfo(
    string Ref,
    string Level,
    string? Database,
    string Name,
    string? Host,
    string Type,
    bool Enabled,
    bool Locked,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ModifiedAt,
    string? DefaultDatabase,
    string? DefaultSchema,
    string? LoginRef,
    bool IsOrphaned,
    SqlPasswordInfo? Password);

/// <summary>Password attributes only (policy, expiration). Hashes and passwords are never read (RF-6).</summary>
public sealed record SqlPasswordInfo(bool? PolicyEnforced, bool? ExpirationEnforced, bool? Expired, DateTimeOffset? ValidUntil);

public sealed record SqlRoleMembershipInfo(string MemberRef, string RoleRef);

public sealed record SqlPermissionInfo(string GranteeRef, SqlSecurableInfo Securable, string Permission, string State, string? GrantorRef);

public sealed record SqlSecurableInfo(string Class, string? Database, string? Schema, string? Name, string? ObjectType, string? Column);

public sealed record SqlOmittedInfo(int BuiltinPublicPermissions);

public sealed record SqlUnavailableSection(string Section, string Reason);

/// <summary>Closed values of the contract.</summary>
public static class SqlEngines
{
    public const string SqlServer = "sqlserver";
    public const string PostgreSql = "postgresql";
    public const string MySql = "mysql";
    public const string MariaDb = "mariadb";

    public static IReadOnlyList<string> All { get; } = [SqlServer, PostgreSql, MySql, MariaDb];
}

public static class SqlTlsModes
{
    /// <summary>TLS validated against the OS trust store and the host name (default).</summary>
    public const string Verify = "verify";

    /// <summary>TLS accepting only the pinned certificate (file or SHA-256).</summary>
    public const string Pinned = "pinned";

    /// <summary>TLS without certificate validation (ADR-0015): only after the administrator's express confirmation.</summary>
    public const string Unverified = "unverified";

    public static IReadOnlyList<string> All { get; } = [Verify, Pinned, Unverified];
}

public static class SqlAuthenticationModes
{
    public const string Mixed = "mixed";
    public const string Windows = "windows";
    public const string Password = "password";
    public const string Unknown = "unknown";
}

public static class SqlAccountLevels
{
    public const string Server = "server";
    public const string Database = "database";
}

public static class SqlAccountTypes
{
    public const string SqlLogin = "sql-login";
    public const string WindowsLogin = "windows-login";
    public const string WindowsGroup = "windows-group";
    public const string EntraLogin = "entra-login";
    public const string ServerRole = "server-role";
    public const string DatabaseUser = "database-user";
    public const string DatabaseRole = "database-role";
    public const string ApplicationRole = "application-role";
    public const string UserWithoutLogin = "user-without-login";
    public const string PgRole = "pg-role";
    public const string MySqlAccount = "mysql-account";
    public const string MySqlRole = "mysql-role";
    public const string Certificate = "certificate";
    public const string Other = "other";

    /// <summary>Account types that can be the role of a membership (contract §1).</summary>
    public static bool IsRole(string type) =>
        type is ServerRole or DatabaseRole or ApplicationRole or PgRole or MySqlRole;
}

public static class SqlSecurableClasses
{
    public const string Server = "server";
    public const string Database = "database";
    public const string Schema = "schema";
    public const string Object = "object";
    public const string Column = "column";
    public const string DefaultPrivileges = "default-privileges";
}

public static class SqlObjectTypes
{
    public const string Table = "table";
    public const string View = "view";
    public const string MaterializedView = "materialized-view";
    public const string ForeignTable = "foreign-table";
    public const string Procedure = "procedure";
    public const string Function = "function";
    public const string Aggregate = "aggregate";
    public const string Sequence = "sequence";
    public const string Type = "type";
    public const string Domain = "domain";
    public const string Synonym = "synonym";
    public const string Schema = "schema";
    public const string Database = "database";
    public const string Language = "language";
    public const string LargeObject = "large-object";
    public const string ForeignDataWrapper = "foreign-data-wrapper";
    public const string ForeignServer = "foreign-server";
    public const string Tablespace = "tablespace";
    public const string Parameter = "parameter";
    public const string Assembly = "assembly";
    public const string XmlSchemaCollection = "xml-schema-collection";
    public const string Certificate = "certificate";
    public const string AsymmetricKey = "asymmetric-key";
    public const string SymmetricKey = "symmetric-key";
    public const string FullTextCatalog = "full-text-catalog";
    public const string ServiceBroker = "service-broker";
    public const string Login = "login";
    public const string ServerRole = "server-role";
    public const string DatabaseRole = "database-role";
    public const string ApplicationRole = "application-role";
    public const string User = "user";
    public const string Endpoint = "endpoint";
    public const string AvailabilityGroup = "availability-group";
    public const string Event = "event";
    public const string Other = "other";
}

public static class SqlPermissionStates
{
    public const string Grant = "grant";
    public const string GrantWithGrantOption = "grant-with-grant-option";
    public const string Deny = "deny";
}

public static class SqlPermissionNames
{
    /// <summary>Ownership is sent as this permission, with state grant and no grantor (contract §1).</summary>
    public const string Ownership = "OWNERSHIP";
}

public static class SqlDatabaseStates
{
    public const string Online = "online";
    public const string Offline = "offline";
    public const string Restoring = "restoring";
    public const string Recovering = "recovering";
    public const string Suspect = "suspect";
    public const string Emergency = "emergency";
    public const string Unknown = "unknown";
}

public static class SqlUnavailableReasons
{
    public const string ConnectionFailed = "connection-failed";
    public const string CertificateInvalid = "certificate-invalid";
    public const string AuthenticationFailed = "authentication-failed";
    public const string PermissionDenied = "permission-denied";
    public const string Offline = "offline";
    public const string Timeout = "timeout";
    public const string Error = "error";
}

/// <summary>Sections of a snapshot that can be marked unavailable (RF-7, contract §1).</summary>
public static class SqlSections
{
    public const string Instance = "instance";
    public const string Server = "server";
    private const string DatabasePrefix = "database:";

    public static string Database(string name) => DatabasePrefix + name;
}

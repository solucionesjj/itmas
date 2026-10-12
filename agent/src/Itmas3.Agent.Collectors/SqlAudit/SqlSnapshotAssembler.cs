using System.Text.RegularExpressions;
using Itmas3.Agent.Collectors.Common;
using Itmas3.Agent.Collectors.SqlAudit.Model;

namespace Itmas3.Agent.Collectors.SqlAudit;

/// <summary>
/// Builds the sql-audit v1 snapshot from an engine-neutral <see cref="SqlCatalog"/> (pure, no I/O). Everything that comes
/// from an engine is untrusted text (RNF-8): it is sanitized like the inventory (no control characters, at most 256
/// characters), duplicates are merged by the identities of RN-2 and RN-3, references are resolved to refs, elements that
/// break a rule of the contract are dropped instead of being invented, and the collection limits are respected by
/// marking whole databases as unavailable rather than truncating them (so the server never treats them as removed).
/// </summary>
public static partial class SqlSnapshotAssembler
{
    public const int MaxDatabases = 1000;
    public const int MaxAccounts = 20_000;
    public const int MaxMemberships = 100_000;
    public const int MaxPermissions = 200_000;

    [GeneratedRegex("^[A-Z][A-Z0-9_ ]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex PermissionPattern();

    /// <summary>Snapshot of an instance that could not be read at all (RF-7): only the connection and the reason.</summary>
    public static SqlAuditSnapshot Unavailable(
        SqlConnectionInfo connection, Guid submissionId, DateTimeOffset collectedAt, string agentVersion, string reason) =>
        new(
            SqlAuditSnapshot.CurrentSchemaVersion,
            submissionId,
            collectedAt,
            agentVersion,
            CleanConnection(connection),
            null,
            [],
            [],
            [],
            [],
            new SqlOmittedInfo(0),
            [new SqlUnavailableSection(SqlSections.Instance, reason)]);

    public static SqlAuditSnapshot Assemble(
        SqlConnectionInfo connection, SqlCatalog catalog, Guid submissionId, DateTimeOffset collectedAt, string agentVersion)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var state = new State();

        foreach (var database in catalog.Databases)
        {
            if (Clean(database.Name) is { } name && state.Databases.Count < MaxDatabases && state.DatabaseNames.Add(name))
            {
                state.Databases.Add((name, database.State, database.OwnerKey));
            }
        }

        foreach (var unavailable in catalog.Unavailable)
        {
            state.MarkUnavailable(unavailable.Section, unavailable.Reason);
        }

        // The server section goes first: database sections reference its accounts (logins, PostgreSQL and MySQL roles).
        foreach (var section in catalog.Sections.OrderBy(section => section.Section == SqlSections.Server ? 0 : 1))
        {
            // A section may name a database that is not listed (e.g. a MySQL grant on a pattern such as app\_%): its
            // permissions are still sent; only database-level accounts need a listed database.
            var database = DatabaseOf(section.Section);
            if (database is not null && state.UnavailableSections.Contains(SqlSections.Database(database)))
            {
                continue;
            }

            if (!state.TryAdd(section) && database is not null)
            {
                // Over the contract limits: the whole database is reported as unavailable, never truncated.
                state.MarkUnavailable(SqlSections.Database(database), SqlUnavailableReasons.Error);
            }
        }

        var databases = state.Databases
            .Select(database => new SqlDatabaseInfo(database.Name, database.State, database.OwnerKey is null ? null : state.Resolve(database.OwnerKey)))
            .ToList();

        return new SqlAuditSnapshot(
            SqlAuditSnapshot.CurrentSchemaVersion,
            submissionId,
            collectedAt,
            agentVersion,
            CleanConnection(connection),
            new SqlInstanceInfo(
                Clean(catalog.Instance.Version) ?? "unknown",
                Clean(catalog.Instance.Edition),
                catalog.Instance.AuthenticationMode),
            databases,
            state.Accounts,
            state.Memberships,
            state.Permissions,
            new SqlOmittedInfo(state.Omitted),
            state.Unavailable);
    }

    /// <summary>Contract text: no control characters, trimmed, at most 256 characters; null when nothing is left.</summary>
    public static string? Clean(string? value) => TextSanitizer.Clean(value);

    /// <summary>Upper-case permission name accepted by the contract, or null.</summary>
    public static string? NormalizePermission(string? value)
    {
        var cleaned = Clean(value)?.ToUpperInvariant();
        return cleaned is not null && PermissionPattern().IsMatch(cleaned) ? cleaned : null;
    }

    private static SqlConnectionInfo CleanConnection(SqlConnectionInfo connection) =>
        connection with { Host = Clean(connection.Host) ?? connection.Host, InstanceName = Clean(connection.InstanceName) };

    private static string? DatabaseOf(string section) =>
        section.StartsWith("database:", StringComparison.Ordinal) ? Clean(section["database:".Length..]) : null;

    private sealed class State
    {
        private readonly Dictionary<string, string> _refsByKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SqlAccountInfo> _accountsByRef = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _refsByIdentity = new(StringComparer.Ordinal);
        private readonly HashSet<string> _membershipIdentities = new(StringComparer.Ordinal);
        private readonly HashSet<string> _permissionIdentities = new(StringComparer.Ordinal);

        public List<(string Name, string State, string? OwnerKey)> Databases { get; } = [];

        public HashSet<string> DatabaseNames { get; } = new(StringComparer.Ordinal);

        public HashSet<string> UnavailableSections { get; } = new(StringComparer.Ordinal);

        public List<SqlUnavailableSection> Unavailable { get; } = [];

        public List<SqlAccountInfo> Accounts { get; } = [];

        public List<SqlRoleMembershipInfo> Memberships { get; } = [];

        public List<SqlPermissionInfo> Permissions { get; } = [];

        public int Omitted { get; private set; }

        public string? Resolve(string key) => _refsByKey.GetValueOrDefault(key);

        public void MarkUnavailable(string section, string reason)
        {
            var database = DatabaseOf(section);
            var cleaned = database is null ? section : SqlSections.Database(database);
            var valid = cleaned is SqlSections.Server or SqlSections.Instance || (database is not null && DatabaseNames.Contains(database));
            if (valid && UnavailableSections.Add(cleaned))
            {
                Unavailable.Add(new SqlUnavailableSection(cleaned, reason));
            }
        }

        /// <summary>Adds a section atomically: either all its valid elements fit the limits, or nothing is added.</summary>
        public bool TryAdd(CatalogSection section)
        {
            var accounts = new List<SqlAccountInfo>();
            var sources = new List<CatalogAccount>();
            var newAccounts = new Dictionary<string, SqlAccountInfo>(StringComparer.Ordinal);
            var newRefs = new Dictionary<string, string>(StringComparer.Ordinal);
            var newIdentities = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var account in section.Accounts)
            {
                if (ToAccount(account, Accounts.Count + accounts.Count) is not { } candidate)
                {
                    continue;
                }

                var identity = string.Join('\u001f', candidate.Level, candidate.Database, candidate.Name, candidate.Host, candidate.Type);
                if (_refsByIdentity.TryGetValue(identity, out var existing) || newIdentities.TryGetValue(identity, out existing))
                {
                    newRefs[account.Key] = existing;
                    continue;
                }

                newIdentities[identity] = candidate.Ref;
                newRefs[account.Key] = candidate.Ref;
                accounts.Add(candidate);
                sources.Add(account);
                newAccounts[candidate.Ref] = candidate;
            }

            string? Lookup(string? key) => key is null ? null : newRefs.GetValueOrDefault(key) ?? _refsByKey.GetValueOrDefault(key);

            SqlAccountInfo? AccountOf(string reference) =>
                _accountsByRef.GetValueOrDefault(reference) ?? newAccounts.GetValueOrDefault(reference);

            // Logins of database users must be server-level accounts.
            for (var index = 0; index < accounts.Count; index++)
            {
                var loginRef = Lookup(sources[index].LoginKey);
                accounts[index] = accounts[index] with
                {
                    LoginRef = loginRef is not null && AccountOf(loginRef)?.Level == SqlAccountLevels.Server ? loginRef : null,
                };
            }

            var memberships = new List<SqlRoleMembershipInfo>();
            var membershipIdentities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var membership in section.Memberships)
            {
                var member = Lookup(membership.MemberKey);
                var role = Lookup(membership.RoleKey);
                if (member is null || role is null || member == role || AccountOf(role) is not { } roleAccount || !SqlAccountTypes.IsRole(roleAccount.Type))
                {
                    continue;
                }

                var identity = member + '\u001f' + role;
                if (!_membershipIdentities.Contains(identity) && membershipIdentities.Add(identity))
                {
                    memberships.Add(new SqlRoleMembershipInfo(member, role));
                }
            }

            var permissions = new List<SqlPermissionInfo>();
            var permissionIdentities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var permission in section.Permissions)
            {
                if (ToPermission(permission, Lookup) is not { } candidate)
                {
                    continue;
                }

                var securable = candidate.Securable;
                var identity = string.Join(
                    '\u001f', candidate.GranteeRef, securable.Class, securable.Database, securable.Schema, securable.Name, securable.ObjectType, securable.Column, candidate.Permission, candidate.State);
                if (!_permissionIdentities.Contains(identity) && permissionIdentities.Add(identity))
                {
                    permissions.Add(candidate);
                }
            }

            if (Accounts.Count + accounts.Count > MaxAccounts
                || Memberships.Count + memberships.Count > MaxMemberships
                || Permissions.Count + permissions.Count > MaxPermissions)
            {
                return false;
            }

            foreach (var (key, reference) in newRefs)
            {
                _refsByKey[key] = reference;
            }

            foreach (var (identity, reference) in newIdentities)
            {
                _refsByIdentity[identity] = reference;
            }

            foreach (var account in accounts)
            {
                _accountsByRef[account.Ref] = account;
            }

            _membershipIdentities.UnionWith(membershipIdentities);
            _permissionIdentities.UnionWith(permissionIdentities);
            Accounts.AddRange(accounts);
            Memberships.AddRange(memberships);
            Permissions.AddRange(permissions);
            Omitted += Math.Max(0, section.OmittedBuiltinPublic);
            return true;
        }

        private SqlAccountInfo? ToAccount(CatalogAccount account, int index)
        {
            // A MySQL/MariaDB anonymous account (''@host) is a finding in itself: it keeps its empty name.
            var name = account.Type == SqlAccountTypes.MySqlAccount && account.Name?.Length == 0 ? string.Empty : Clean(account.Name);
            var database = Clean(account.Database);
            var level = account.Level;
            var valid = name is not null
                && (level == SqlAccountLevels.Server ? database is null : level == SqlAccountLevels.Database && database is not null && DatabaseNames.Contains(database));
            if (!valid)
            {
                return null;
            }

            return new SqlAccountInfo(
                $"a{index + 1}",
                level,
                database,
                name!,
                Clean(account.Host),
                account.Type,
                account.Enabled,
                account.Locked,
                account.CreatedAt,
                account.ModifiedAt,
                Clean(account.DefaultDatabase),
                Clean(account.DefaultSchema),
                null,
                account.IsOrphaned,
                account.Password);
        }

        private static SqlPermissionInfo? ToPermission(CatalogPermission permission, Func<string?, string?> lookup)
        {
            var grantee = lookup(permission.GranteeKey);
            var name = NormalizePermission(permission.Permission);
            var securable = CleanSecurable(permission.Securable);
            if (grantee is null || name is null || securable is null
                || permission.State is not (SqlPermissionStates.Grant or SqlPermissionStates.GrantWithGrantOption or SqlPermissionStates.Deny))
            {
                return null;
            }

            return new SqlPermissionInfo(grantee, securable, name, permission.State, lookup(permission.GrantorKey));
        }

        /// <summary>Applies the per-class rules of the contract (§1); null when the securable cannot be expressed.</summary>
        private static SqlSecurableInfo? CleanSecurable(SqlSecurableInfo securable)
        {
            var cleaned = securable with
            {
                Database = Clean(securable.Database),
                Schema = Clean(securable.Schema),
                Name = Clean(securable.Name),
                ObjectType = Clean(securable.ObjectType),
                Column = Clean(securable.Column),
            };

            var valid = cleaned.Class switch
            {
                SqlSecurableClasses.Server => cleaned is { Database: null, Schema: null, Column: null } && (cleaned.Name is null ? cleaned.ObjectType is null : cleaned.ObjectType is not null),
                SqlSecurableClasses.Database => cleaned is { Database: not null, Schema: null, Name: null, ObjectType: null, Column: null },
                SqlSecurableClasses.Schema => cleaned is { Database: not null, Schema: not null, Name: null, ObjectType: null, Column: null },
                SqlSecurableClasses.Object => cleaned is { Database: not null, Name: not null, ObjectType: not null, Column: null },
                SqlSecurableClasses.Column => cleaned is { Database: not null, Name: not null, ObjectType: not null, Column: not null },
                SqlSecurableClasses.DefaultPrivileges => cleaned is { Database: not null, Name: null, ObjectType: not null, Column: null },
                _ => false,
            };
            return valid ? cleaned : null;
        }
    }
}

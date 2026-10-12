using Itmas3.Agent.Collectors.SqlAudit.Model;

namespace Itmas3.Agent.Collectors.SqlAudit;

// Engine-neutral result of a collection, produced by the pure mappers of each engine. Elements reference each other by
// engine keys (e.g. "s:259", "pg:16384", "mysql:app@%"); SqlSnapshotAssembler turns them into contract refs, sanitizes the
// text and removes duplicates. Every element belongs to one section (contract §1) so that a database that could not be
// read only affects its own elements (RF-7, RF-13).

/// <param name="Instance">Version, edition and authentication mode.</param>
/// <param name="Databases">All databases of the instance, readable or not.</param>
/// <param name="Sections">The server section and one section per database that could be read.</param>
/// <param name="Unavailable">Sections that could not be read, with their reason.</param>
public sealed record SqlCatalog(
    SqlInstanceInfo Instance,
    IReadOnlyList<CatalogDatabase> Databases,
    IReadOnlyList<CatalogSection> Sections,
    IReadOnlyList<SqlUnavailableSection> Unavailable);

public sealed record CatalogDatabase(string Name, string State, string? OwnerKey);

/// <param name="Section">"server" or "database:&lt;name&gt;".</param>
/// <param name="OmittedBuiltinPublic">Built-in permissions granted to public that were left out (RF-5).</param>
public sealed record CatalogSection(
    string Section,
    IReadOnlyList<CatalogAccount> Accounts,
    IReadOnlyList<CatalogMembership> Memberships,
    IReadOnlyList<CatalogPermission> Permissions,
    int OmittedBuiltinPublic);

public sealed record CatalogAccount(
    string Key,
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
    string? LoginKey,
    bool IsOrphaned,
    SqlPasswordInfo? Password);

public sealed record CatalogMembership(string MemberKey, string RoleKey);

public sealed record CatalogPermission(string GranteeKey, SqlSecurableInfo Securable, string Permission, string State, string? GrantorKey);

/// <summary>Securable builders that respect the per-class rules of the contract (§1).</summary>
public static class Securables
{
    public static SqlSecurableInfo Server() => new(SqlSecurableClasses.Server, null, null, null, null, null);

    public static SqlSecurableInfo ServerObject(string name, string objectType) =>
        new(SqlSecurableClasses.Server, null, null, name, objectType, null);

    public static SqlSecurableInfo Database(string database) => new(SqlSecurableClasses.Database, database, null, null, null, null);

    public static SqlSecurableInfo Schema(string database, string schema) => new(SqlSecurableClasses.Schema, database, schema, null, null, null);

    public static SqlSecurableInfo Object(string database, string? schema, string name, string objectType) =>
        new(SqlSecurableClasses.Object, database, schema, name, objectType, null);

    public static SqlSecurableInfo Column(string database, string? schema, string name, string objectType, string column) =>
        new(SqlSecurableClasses.Column, database, schema, name, objectType, column);

    public static SqlSecurableInfo DefaultPrivileges(string database, string? schema, string objectType) =>
        new(SqlSecurableClasses.DefaultPrivileges, database, schema, null, objectType, null);
}

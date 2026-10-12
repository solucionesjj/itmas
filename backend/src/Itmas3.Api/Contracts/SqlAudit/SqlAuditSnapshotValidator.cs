using System.ComponentModel.DataAnnotations;
using Itmas3.Application.SqlAudit;
using Itmas3.Domain.SqlAudit;

namespace Itmas3.Api.Contracts.SqlAudit;

/// <summary>
/// Cross-field and large-collection rules of the snapshot contract (SPEC-0005 §12.2 and the contract notes): closed
/// enumerations, safe text, references that exist, unique identities (RN-2, RN-3), securable shape per class, and
/// <c>instance</c> null if and only if the section <c>instance</c> is unavailable. Everything coming from an engine is
/// untrusted (RNF-8).
/// </summary>
internal sealed class SqlAuditSnapshotValidator(SqlAuditSnapshotRequest request)
{
    private static readonly HashSet<string> AccountTypes = new(SqlAuditCodes.AccountTypes, StringComparer.Ordinal);
    private static readonly HashSet<string> ObjectTypes = new(SqlAuditCodes.ObjectTypes, StringComparer.Ordinal);
    private static readonly HashSet<string> RoleTypes = new(StringComparer.Ordinal) { "server-role", "database-role", "application-role", "pg-role", "mysql-role" };

    private readonly List<ValidationResult> _errors = [];

    public IEnumerable<ValidationResult> Validate()
    {
        if (request.Connection is null || request.Databases is null || request.Unavailable is null || request.Omitted is null)
        {
            return _errors;
        }

        if (request.Accounts is null || request.RoleMemberships is null || request.Permissions is null)
        {
            Error("Las colecciones accounts, roleMemberships y permissions son obligatorias.", nameof(request.Accounts));
            return _errors;
        }

        if (request.Accounts.Count > SqlAuditContract.MaxAccounts
            || request.RoleMemberships.Count > SqlAuditContract.MaxMemberships
            || request.Permissions.Count > SqlAuditContract.MaxPermissions)
        {
            Error("La instantánea supera el número máximo de cuentas, pertenencias o permisos.", nameof(request.Accounts));
            return _errors;
        }

        var databases = ValidateDatabases();
        ValidateUnavailable(databases);
        var accounts = ValidateAccounts(databases);
        ValidateDatabaseOwners(accounts);
        ValidateMemberships(accounts);
        ValidatePermissions(accounts, databases);
        return _errors;
    }

    private HashSet<string> ValidateDatabases()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var database in request.Databases)
        {
            if (database?.Name is { } name && !names.Add(name))
            {
                Error("Una base de datos está repetida.", nameof(request.Databases));
            }
        }

        return names;
    }

    private void ValidateUnavailable(HashSet<string> databases)
    {
        var sections = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in request.Unavailable)
        {
            if (part?.Section is not { } section)
            {
                continue;
            }

            if (!sections.Add(section))
            {
                Error("Una sección no disponible está repetida.", nameof(request.Unavailable));
            }

            var known = section is SqlUnavailablePart.InstanceSection or SqlUnavailablePart.ServerSection
                || (section.StartsWith(SqlUnavailablePart.DatabaseSectionPrefix, StringComparison.Ordinal)
                    && databases.Contains(section[SqlUnavailablePart.DatabaseSectionPrefix.Length..]));
            if (!known)
            {
                Error("Una sección no disponible no es instance, server ni una base de datos de la instantánea.", nameof(request.Unavailable));
            }
        }

        var connectionFailed = sections.Contains(SqlUnavailablePart.InstanceSection);
        if (connectionFailed != (request.Instance is null))
        {
            Error("La sección instance debe ser nula si y solo si se declara como no disponible.", nameof(request.Instance));
        }

        if (connectionFailed && (request.Databases.Count > 0 || request.Accounts.Count > 0 || request.RoleMemberships.Count > 0 || request.Permissions.Count > 0))
        {
            Error("Una instantánea sin conexión no puede incluir bases de datos, cuentas ni permisos.", nameof(request.Instance));
        }
    }

    private Dictionary<string, SqlAccountEntry> ValidateAccounts(HashSet<string> databases)
    {
        var byRef = new Dictionary<string, SqlAccountEntry>(StringComparer.Ordinal);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var account in request.Accounts)
        {
            if (account is null)
            {
                Error("Una cuenta es nula.", nameof(request.Accounts));
                continue;
            }

            // A MySQL/MariaDB anonymous account (''@host) has an empty name.
            var anonymous = account.Name is { Length: 0 } && account.Type == SqlAuditCodes.AnonymousAccountType;
            var valid = Reference(account.Ref, "accounts.ref")
                & (anonymous || Text(account.Name, "accounts.name"))
                & OptionalText(account.Host, "accounts.host")
                & OptionalText(account.DefaultDatabase, "accounts.defaultDatabase")
                & OptionalText(account.DefaultSchema, "accounts.defaultSchema")
                & Check(AccountTypes.Contains(account.Type ?? string.Empty), "accounts.type no es un tipo de cuenta conocido.");

            var level = SqlAuditCodes.ParseLevel(account.Level);
            valid &= Check(level is not null, "accounts.level debe ser server o database.");
            valid &= Check(
                level == SqlAccountLevel.Database
                    ? account.Database is { } database && databases.Contains(database)
                    : account.Database is null,
                "accounts.database debe ser una base de datos de la instantánea si y solo si level es database.");

            if (account.Ref is not null && !byRef.TryAdd(account.Ref, account))
            {
                valid = Check(false, "accounts.ref está repetido.");
            }

            if (valid && level is { } accountLevel
                && !identities.Add(SqlIdentity.Account(accountLevel, account.Database, account.Name, account.Host, account.Type!)))
            {
                Error("Una cuenta está repetida (mismo nivel, base de datos, nombre, host y tipo).", nameof(request.Accounts));
            }

            if (HasTooManyErrors)
            {
                break;
            }
        }

        foreach (var account in request.Accounts)
        {
            if (account?.LoginRef is { } loginRef
                && (!byRef.TryGetValue(loginRef, out var login) || login.Level != "server" || account.Level != "database"))
            {
                Error("accounts.loginRef debe apuntar a una cuenta de servidor desde una cuenta de base de datos.", nameof(request.Accounts));
                break;
            }
        }

        return byRef;
    }

    private void ValidateDatabaseOwners(Dictionary<string, SqlAccountEntry> accounts)
    {
        if (request.Databases.Any(database => database?.OwnerRef is { } owner && !accounts.ContainsKey(owner)))
        {
            Error("databases.ownerRef no corresponde a ninguna cuenta.", nameof(request.Databases));
        }
    }

    private void ValidateMemberships(Dictionary<string, SqlAccountEntry> accounts)
    {
        var pairs = new HashSet<(string, string)>();
        foreach (var membership in request.RoleMemberships)
        {
            if (membership?.MemberRef is not { } memberRef || membership.RoleRef is not { } roleRef
                || !accounts.ContainsKey(memberRef) || !accounts.TryGetValue(roleRef, out var role))
            {
                Error("roleMemberships hace referencia a una cuenta inexistente.", nameof(request.RoleMemberships));
            }
            else if (!RoleTypes.Contains(role.Type) || memberRef == roleRef)
            {
                Error("roleMemberships.roleRef debe apuntar a un rol distinto del miembro.", nameof(request.RoleMemberships));
            }
            else if (!pairs.Add((memberRef, roleRef)))
            {
                Error("Una pertenencia a un rol está repetida.", nameof(request.RoleMemberships));
            }

            if (HasTooManyErrors)
            {
                return;
            }
        }
    }

    private void ValidatePermissions(Dictionary<string, SqlAccountEntry> accounts, HashSet<string> databases)
    {
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var permission in request.Permissions)
        {
            if (permission?.Securable is not { } securable)
            {
                Error("Un permiso no tiene objeto asegurable.", nameof(request.Permissions));
                continue;
            }

            var valid = Check(permission.GranteeRef is { } granteeRef && accounts.ContainsKey(granteeRef), "permissions.granteeRef no corresponde a ninguna cuenta.")
                & Check(permission.GrantorRef is null || accounts.ContainsKey(permission.GrantorRef), "permissions.grantorRef no corresponde a ninguna cuenta.")
                & Check(permission.Permission is { } name && SqlAuditContract.PermissionPattern().IsMatch(name), "permissions.permission no es un permiso válido.")
                & Check(SqlAuditCodes.ParseState(permission.State) is not null, "permissions.state no es válido.")
                & ValidSecurable(securable);

            if (valid)
            {
                var grantee = accounts[permission.GranteeRef];
                var granteeIdentity = SqlIdentity.Account(SqlAuditCodes.ParseLevel(grantee.Level)!.Value, grantee.Database, grantee.Name, grantee.Host, grantee.Type);
                var identity = SqlIdentity.Permission(granteeIdentity, ToSecurable(securable), permission.Permission, SqlAuditCodes.ParseState(permission.State)!.Value);
                if (!identities.Add(identity))
                {
                    Error("Un permiso está repetido (mismo titular, objeto, permiso y estado).", nameof(request.Permissions));
                }
            }

            if (HasTooManyErrors)
            {
                return;
            }
        }
    }

    private bool ValidSecurable(SqlSecurableEntry securable)
    {
        var securableClass = SqlAuditCodes.ParseSecurableClass(securable.Class);
        var texts = OptionalText(securable.Database, "securable.database")
            & OptionalText(securable.Schema, "securable.schema")
            & OptionalText(securable.Name, "securable.name")
            & OptionalText(securable.Column, "securable.column")
            & Check(securable.ObjectType is null || ObjectTypes.Contains(securable.ObjectType), "securable.objectType no es un tipo de objeto conocido.");
        if (!texts || securableClass is null)
        {
            return Check(securableClass is not null, "securable.class no es válida.") && texts;
        }

        // MySQL grants may name a database pattern (app\_%) or a database that does not exist: not necessarily listed.
        var database = securable.Database is not null;
        var shape = securableClass switch
        {
            SqlSecurableClass.Server => securable.Database is null && securable.Schema is null && securable.Column is null
                && (securable.Name is null || securable.ObjectType is not null),
            SqlSecurableClass.Database => database && securable.Schema is null && securable.Name is null && securable.ObjectType is null && securable.Column is null,
            SqlSecurableClass.Schema => database && securable.Schema is not null && securable.Name is null && securable.ObjectType is null && securable.Column is null,
            SqlSecurableClass.Object => database && securable.Name is not null && securable.ObjectType is not null && securable.Column is null,
            SqlSecurableClass.Column => database && securable.Name is not null && securable.ObjectType is not null && securable.Column is not null,
            _ => database && securable.Name is null && securable.ObjectType is not null && securable.Column is null,
        };

        return Check(shape, "securable no tiene los campos que corresponden a su clase.");
    }

    public static SqlSecurable ToSecurable(SqlSecurableEntry securable) => new(
        SqlAuditCodes.ParseSecurableClass(securable.Class)!.Value, securable.Database, securable.Schema, securable.Name, securable.ObjectType, securable.Column);

    private bool HasTooManyErrors => _errors.Count >= SqlAuditContract.MaxReportedErrors;

    private bool Reference(string? value, string field) =>
        Check(value is not null && SqlAuditContract.RefPattern().IsMatch(value), $"{field} no es una referencia válida.");

    private bool Text(string? value, string field) =>
        Check(SqlAuditContract.IsSafeText(value), $"{field} es obligatorio, contiene caracteres no permitidos o supera {SqlAuditContract.TextLength} caracteres.");

    private bool OptionalText(string? value, string field) => value is null || Text(value, field);

    private bool Check(bool condition, string message)
    {
        if (!condition && !HasTooManyErrors)
        {
            Error(message, null);
        }

        return condition;
    }

    private void Error(string message, string? member) =>
        _errors.Add(new ValidationResult(message, member is null ? [] : [member]));
}

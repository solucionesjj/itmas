using Itmas3.Domain.Common;

namespace Itmas3.Domain.Authorization;

/// <summary>Reusable set of permissions that can be assigned to many users. Not a business role.</summary>
public sealed class Policy : Entity<Guid>
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;

    private readonly List<PolicyStatement> _statements = [];

    private Policy(Guid id, string name, string description, DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        Description = description;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        IsActive = true;
    }

    public string Name { get; private set; }

    public string Description { get; private set; }

    /// <summary>An inactive policy keeps its assignments but grants nothing.</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Incremented on every change; used for optimistic concurrency (ETag).</summary>
    public int Revision { get; private set; }

    public IReadOnlyCollection<PolicyStatement> Statements => _statements;

    public IEnumerable<string> PermissionNames => _statements.Select(statement => statement.PermissionName);

    public static Policy Create(string name, string description, IEnumerable<PermissionDefinition> permissions, DateTimeOffset now)
    {
        var policy = new Policy(Guid.CreateVersion7(now), ValidName(name), ValidDescription(description), now);
        policy.ReplacePermissions(permissions);
        policy.Revision = 1;
        return policy;
    }

    public void Update(string name, string description, bool isActive, IEnumerable<PermissionDefinition> permissions, DateTimeOffset now)
    {
        Name = ValidName(name);
        Description = ValidDescription(description);
        IsActive = isActive;
        ReplacePermissions(permissions);
        UpdatedAt = now;
        Revision++;
    }

    private void ReplacePermissions(IEnumerable<PermissionDefinition> permissions)
    {
        var requested = permissions.DistinctBy(permission => permission.Name).ToList();

        var invalid = requested.Where(permission => !permission.IsAssignable).Select(permission => permission.Name).ToList();
        if (invalid.Count > 0)
        {
            throw new DomainException($"Estos permisos no pueden incluirse en una política (obsoletos o no otorgables): {string.Join(", ", invalid)}.");
        }

        _statements.RemoveAll(statement => requested.All(permission => permission.Name != statement.PermissionName));
        foreach (var permission in requested.Where(permission => _statements.All(statement => statement.PermissionName != permission.Name)))
        {
            _statements.Add(new PolicyStatement(permission.Name, PolicyEffect.Allow));
        }
    }

    private static string ValidName(string name) =>
        PermissionDefinition.Require(name, MaxNameLength, "El nombre de la política");

    private static string ValidDescription(string description) =>
        PermissionDefinition.Optional(description, MaxDescriptionLength, "La descripción de la política");
}

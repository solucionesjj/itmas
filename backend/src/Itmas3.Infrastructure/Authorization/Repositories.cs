using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Domain.Authorization;
using Itmas3.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itmas3.Infrastructure.Authorization;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> FindByExternalIdentityAsync(string provider, string issuer, string subject, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(
            user => user.Identities.Any(identity => identity.Provider == provider && identity.Issuer == issuer && identity.Subject == subject),
            cancellationToken);

    public Task<int> CountActiveRootsAsync(CancellationToken cancellationToken) =>
        db.Users.CountAsync(user => user.IsRoot && user.IsActive, cancellationToken);

    public void Add(User user) => db.Users.Add(user);
}

internal sealed class PolicyRepository(AppDbContext db) : IPolicyRepository
{
    public Task<Policy?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Policies.SingleOrDefaultAsync(policy => policy.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken)
    {
#pragma warning disable CA1862 // Translated to SQL lower(); the unique index is defined on lower(name).
        var normalized = name.Trim().ToLowerInvariant();
        return db.Policies.AnyAsync(
            policy => policy.Name.ToLower() == normalized && (excludingId == null || policy.Id != excludingId),
            cancellationToken);
#pragma warning restore CA1862
    }

    public Task<bool> HasAssignmentsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.PolicyAssignments.Any(assignment => assignment.PolicyId == id), cancellationToken);

    public void Add(Policy policy) => db.Policies.Add(policy);

    public void Remove(Policy policy) => db.Policies.Remove(policy);
}

internal sealed class PermissionRepository(AppDbContext db) : IPermissionRepository
{
    public async Task<IReadOnlyList<PermissionDefinition>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.Permissions.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PermissionDefinition>> GetByNamesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken) =>
        await db.Permissions.Where(permission => names.Contains(permission.Id)).ToListAsync(cancellationToken);

    public Task<PermissionDefinition?> GetAsync(string name, CancellationToken cancellationToken) =>
        db.Permissions.SingleOrDefaultAsync(permission => permission.Id == name, cancellationToken);

    public void Add(PermissionDefinition permission) => db.Permissions.Add(permission);
}

using Itmas3.Domain.Authorization;

namespace Itmas3.Application.Authorization.Abstractions;

public interface IUserRepository
{
    Task<User?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> FindByExternalIdentityAsync(string provider, string issuer, string subject, CancellationToken cancellationToken);

    Task<int> CountActiveRootsAsync(CancellationToken cancellationToken);

    void Add(User user);
}

public interface IPolicyRepository
{
    Task<Policy?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken);

    Task<bool> HasAssignmentsAsync(Guid id, CancellationToken cancellationToken);

    void Add(Policy policy);

    void Remove(Policy policy);
}

public interface IPermissionRepository
{
    Task<IReadOnlyList<PermissionDefinition>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PermissionDefinition>> GetByNamesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken);

    Task<PermissionDefinition?> GetAsync(string name, CancellationToken cancellationToken);

    void Add(PermissionDefinition permission);
}

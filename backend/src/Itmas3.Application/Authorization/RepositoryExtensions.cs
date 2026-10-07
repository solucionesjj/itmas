using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Common.Exceptions;
using Itmas3.Domain.Authorization;

namespace Itmas3.Application.Authorization;

internal static class RepositoryExtensions
{
    public static async Task<User> GetRequiredAsync(this IUserRepository users, Guid id, CancellationToken cancellationToken) =>
        await users.GetAsync(id, cancellationToken) ?? throw new NotFoundException($"User {id} not found.");

    public static async Task<Policy> GetRequiredAsync(this IPolicyRepository policies, Guid id, CancellationToken cancellationToken) =>
        await policies.GetAsync(id, cancellationToken) ?? throw new NotFoundException($"Policy {id} not found.");
}

using Microsoft.AspNetCore.Authorization;

namespace Itmas3.Api.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

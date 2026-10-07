using System.ComponentModel.DataAnnotations;
using Itmas3.Application.Authorization;

namespace Itmas3.Api.Contracts.Authorization;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record UpdateUserStatusRequest([Required] bool? IsActive);

public sealed record CreatePolicyRequest(
    [Required, StringLength(100)] string Name,
    [StringLength(500)] string? Description,
    [Required, MaxLength(500)] IReadOnlyList<string> Permissions);

public sealed record UpdatePolicyRequest(
    [Required, StringLength(100)] string Name,
    [StringLength(500)] string? Description,
    [Required] bool? IsActive,
    [Required, MaxLength(500)] IReadOnlyList<string> Permissions);

public sealed record UpdatePermissionRequest(
    [Required, StringLength(100)] string DisplayName,
    [StringLength(500)] string? Description,
    [Required, StringLength(100)] string Category);

public sealed record EffectivePermissionsResponse(Guid UserId, bool IsActive, bool IsRoot, IReadOnlyList<EffectivePermissionResponse> Permissions);

/// <summary>An effective permission and the policies that grant it (SPEC-0001 RF-5, CA-16).</summary>
public sealed record EffectivePermissionResponse(string Permission, IReadOnlyList<PolicyReference> GrantedBy);

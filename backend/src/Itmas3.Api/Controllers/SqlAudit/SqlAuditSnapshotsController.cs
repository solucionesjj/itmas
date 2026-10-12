using Itmas3.Api.Authorization;
using Itmas3.Api.Contracts.SqlAudit;
using Itmas3.Api.Inventory;
using Itmas3.Api.Security;
using Itmas3.Application.SqlAudit;
using Itmas3.Application.SqlAudit.SubmitSnapshot;
using Itmas3.Domain.SqlAudit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Itmas3.Api.Controllers.SqlAudit;

/// <summary>
/// Daily SQL audit snapshots sent by enrolled devices, one per configured connection (SPEC-0005 §8.2). Processed in the
/// request; answers <c>202</c> so a future queue does not change the contract with the agent (RF-9).
/// </summary>
[ApiController]
[Route("api/v1/sql-audit-snapshots")]
public sealed class SqlAuditSnapshotsController(SubmitSqlAuditSnapshotHandler handler) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EnrolledDevice)]
    [EnableRateLimiting(RateLimitingPolicies.SqlAuditSnapshots)]
    [RequestSizeLimit(SqlAuditContract.MaxBodyBytes)]
    [ProducesResponseType<SqlAuditSnapshotAccepted>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Submit(SqlAuditSnapshotRequest request, CancellationToken cancellationToken)
    {
        // RN-1: the device is the authenticated principal, never a value of the payload.
        var deviceId = Guid.Parse(User.FindFirst(DeviceAuthenticationSchemes.DeviceIdClaim)!.Value);
        var connection = request.Connection;

        await handler.HandleAsync(
            new SubmitSqlAuditSnapshotCommand(
                deviceId,
                request.SubmissionId!.Value,
                request.CollectedAt!.Value,
                ConnectionIp.Of(HttpContext),
                connection.Alias,
                SqlInstanceKey.Create(SqlAuditCodes.ParseEngine(connection.Engine)!.Value, connection.Host, connection.Port, connection.InstanceName),
                SqlAuditCodes.ParseTlsMode(connection.Tls)!.Value,
                request.Instance is { } instance ? new SqlInstanceMetadata(instance.Version, instance.Edition, instance.AuthenticationMode) : null,
                request.Databases.Select(database => new SqlSnapshotDatabase(database.Name, database.State, database.OwnerRef)).ToList(),
                request.Accounts.Select(ToAccount).ToList(),
                request.RoleMemberships.Select(membership => new SqlSnapshotMembership(membership.MemberRef, membership.RoleRef)).ToList(),
                request.Permissions.Select(ToPermission).ToList(),
                request.Omitted.BuiltinPublicPermissions,
                request.Unavailable.Select(part => new SqlUnavailablePart(part.Section, part.Reason)).ToList()),
            cancellationToken);

        return Accepted(new SqlAuditSnapshotAccepted(request.SubmissionId.Value));
    }

    private static SqlSnapshotAccount ToAccount(SqlAccountEntry account) => new(
        account.Ref,
        SqlAuditCodes.ParseLevel(account.Level)!.Value,
        account.Database,
        account.Name,
        account.Host,
        account.Type,
        new SqlAccountAttributes(
            account.Enabled,
            account.Locked,
            account.CreatedAt,
            account.ModifiedAt,
            account.DefaultDatabase,
            account.DefaultSchema,
            null,
            account.IsOrphaned,
            account.Password is { } password
                ? new SqlPasswordAttributes(password.PolicyEnforced, password.ExpirationEnforced, password.Expired, password.ValidUntil)
                : null),
        account.LoginRef);

    private static SqlSnapshotPermission ToPermission(SqlPermissionEntry permission) => new(
        permission.GranteeRef,
        SqlAuditSnapshotValidator.ToSecurable(permission.Securable),
        permission.Permission,
        SqlAuditCodes.ParseState(permission.State)!.Value,
        permission.GrantorRef);
}

using System.ComponentModel.DataAnnotations;
using Itmas3.Api.Authorization;
using Itmas3.Api.Contracts.Authorization;
using Itmas3.Api.Contracts.Inventory;
using Itmas3.Api.Contracts.SqlAudit;
using Itmas3.Api.Controllers.Authorization;
using Itmas3.Application.Common;
using Itmas3.Application.SqlAudit;
using Itmas3.Application.SqlAudit.Queries;
using Itmas3.Application.SqlAudit.Validation;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.Api.Controllers.SqlAudit;

/// <summary>Audited instances, their accounts and their permissions (SPEC-0005 RF-20, RF-21, RF-23).</summary>
[ApiController]
[Route("api/v1/sql-instances")]
public sealed class SqlInstancesController(ISqlAuditQueries queries) : ControllerBase
{
    [HttpGet]
    [RequirePermission(SqlAuditPermissions.Read)]
    public async Task<PagedResponse<SqlInstanceListItem>> Search(
        [FromQuery, SafeText(100)] string? search,
        [FromQuery, RegularExpression(SqlAuditQueryContract.EnginePattern)] string? engine,
        [FromQuery, RegularExpression(SqlAuditQueryContract.InstanceSortPattern)] string? sort,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, PagingDefaults.MaxPageSize)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var sortBy = sort?.TrimStart('-') switch
        {
            "lastCollectedAt" => SqlInstanceSort.LastCollectedAt,
            "pending" => SqlInstanceSort.Pending,
            "pendingCritical" => SqlInstanceSort.PendingCritical,
            _ => SqlInstanceSort.Alias,
        };

        var result = await queries.SearchInstancesAsync(
            new SqlInstanceSearch(search, engine, sortBy, sort?.StartsWith('-') == true, PagingDefaults.Page(page, pageSize)), cancellationToken);
        return SqlAuditPaging.Response(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(SqlAuditPermissions.Read)]
    public async Task<ActionResult<SqlInstanceDetail>> Get(Guid id, CancellationToken cancellationToken) =>
        await queries.GetInstanceAsync(id, cancellationToken) is { } instance ? instance : NotFound();

    [HttpGet("{id:guid}/accounts")]
    [RequirePermission(SqlAuditPermissions.Read)]
    public async Task<ActionResult<PagedResponse<SqlAccountListItem>>> Accounts(
        Guid id,
        [FromQuery, RegularExpression(SqlAuditQueryContract.LevelPattern)] string? level,
        [FromQuery, SafeText(256)] string? database,
        [FromQuery, RegularExpression(SqlAuditQueryContract.CodePattern)] string? type,
        [FromQuery, RegularExpression(SqlAuditQueryContract.AccountRiskPattern)] string? risk,
        [FromQuery, RegularExpression(SqlAuditQueryContract.AccountValidationPattern)] string? validation,
        [FromQuery, SafeText(100)] string? search,
        [FromQuery, RegularExpression(SqlAuditQueryContract.AccountSortPattern)] string? sort,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, PagingDefaults.MaxPageSize)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var validationFilter = validation switch
        {
            "pending" => SqlAccountValidationFilter.Pending,
            "valid" => SqlAccountValidationFilter.Valid,
            "changed" => SqlAccountValidationFilter.Changed,
            "removed" => SqlAccountValidationFilter.Removed,
            _ => SqlAccountValidationFilter.Current,
        };
        var sortBy = sort?.TrimStart('-') switch
        {
            "risk" => SqlAccountSort.Risk,
            "pending" => SqlAccountSort.Pending,
            "firstSeenAt" => SqlAccountSort.FirstSeenAt,
            _ => SqlAccountSort.Name,
        };

        var result = await queries.SearchAccountsAsync(
            id,
            new SqlAccountSearch(level, database, type, risk, validationFilter, search, sortBy, sort?.StartsWith('-') == true, PagingDefaults.Page(page, pageSize)),
            cancellationToken);
        return result is null ? NotFound() : SqlAuditPaging.Response(result);
    }

    [HttpGet("{id:guid}/permissions")]
    [RequirePermission(SqlAuditPermissions.Read)]
    public async Task<ActionResult<PagedResponse<SqlAccessItemView>>> Permissions(
        Guid id,
        [FromQuery, RegularExpression(SqlAuditQueryContract.ItemRiskPattern)] string? risk,
        [FromQuery, RegularExpression(SqlAuditQueryContract.ItemValidationPattern)] string? validation,
        [FromQuery, SafeText(256)] string? database,
        [FromQuery, SafeText(100)] string? account,
        [FromQuery, RegularExpression(SqlAuditQueryContract.CodePattern)] string? objectType,
        [FromQuery, SafeText(100)] string? search,
        [FromQuery, RegularExpression(SqlAuditQueryContract.ItemSortPattern)] string? sort,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, PagingDefaults.MaxPageSize)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await queries.SearchPermissionsAsync(
            id,
            SqlAuditPaging.ItemSearch(null, risk, validation, database, account, objectType, search, sort, page, pageSize),
            cancellationToken);
        return result is null ? NotFound() : SqlAuditPaging.Response(result);
    }
}

/// <summary>An account: detail, roles and permissions, history and validation (RF-16, RF-22).</summary>
[ApiController]
[Route("api/v1/sql-accounts")]
public sealed class SqlAccountsController(ISqlAuditQueries queries) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [RequirePermission(SqlAuditPermissions.Read)]
    public async Task<ActionResult<SqlAccountDetail>> Get(Guid id, CancellationToken cancellationToken) =>
        await queries.GetAccountAsync(id, cancellationToken) is { } account ? account : NotFound();

    [HttpGet("{id:guid}/items")]
    [RequirePermission(SqlAuditPermissions.Read)]
    public async Task<ActionResult<PagedResponse<SqlAccessItemView>>> Items(
        Guid id,
        [FromQuery, RegularExpression(SqlAuditQueryContract.KindPattern)] string? kind,
        [FromQuery, RegularExpression(SqlAuditQueryContract.ItemRiskPattern)] string? risk,
        [FromQuery, RegularExpression(SqlAuditQueryContract.ItemValidationPattern)] string? validation,
        [FromQuery, SafeText(256)] string? database,
        [FromQuery, RegularExpression(SqlAuditQueryContract.CodePattern)] string? objectType,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, PagingDefaults.MaxPageSize)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await queries.SearchAccountItemsAsync(
            id, SqlAuditPaging.ItemSearch(kind, risk, validation, database, null, objectType, null, null, page, pageSize), cancellationToken);
        return result is null ? NotFound() : SqlAuditPaging.Response(result);
    }

    [HttpGet("{id:guid}/changes")]
    [RequirePermission(SqlAuditPermissions.Read)]
    public async Task<ActionResult<PagedResponse<SqlAccountHistoryEntry>>> Changes(
        Guid id,
        [FromQuery, Range(1, 100)] int page = 1,
        [FromQuery, Range(1, PagingDefaults.MaxPageSize)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await queries.GetAccountHistoryAsync(id, PagingDefaults.Page(page, pageSize), cancellationToken);
        return result is null ? NotFound() : SqlAuditPaging.Response(result);
    }

    [HttpPut("{id:guid}/validation")]
    [RequirePermission(SqlAuditPermissions.AccessesValidate)]
    public async Task<SqlValidationResponse> Validate(
        Guid id, ValidateSqlAccountRequest request, [FromServices] ValidateSqlAccountHandler handler, CancellationToken cancellationToken) =>
        SqlAuditPaging.Response(await handler.HandleAsync(id, request.Fingerprint, cancellationToken));

    [HttpDelete("{id:guid}/validation")]
    [RequirePermission(SqlAuditPermissions.AccessesValidate)]
    public async Task<IActionResult> Unvalidate(Guid id, [FromServices] UnvalidateSqlAccountHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return NoContent();
    }
}

/// <summary>Validation of a role membership or a permission (RF-16).</summary>
[ApiController]
[Route("api/v1/sql-access-items")]
public sealed class SqlAccessItemsController : ControllerBase
{
    [HttpPut("{id:guid}/validation")]
    [RequirePermission(SqlAuditPermissions.AccessesValidate)]
    public async Task<SqlValidationResponse> Validate(Guid id, [FromServices] ValidateSqlAccessItemHandler handler, CancellationToken cancellationToken) =>
        SqlAuditPaging.Response(await handler.HandleAsync(id, cancellationToken));

    [HttpDelete("{id:guid}/validation")]
    [RequirePermission(SqlAuditPermissions.AccessesValidate)]
    public async Task<IActionResult> Unvalidate(Guid id, [FromServices] UnvalidateSqlAccessItemHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return NoContent();
    }
}

internal static class SqlAuditPaging
{
    public static PagedResponse<T> Response<T>(PagedResult<T> result) => new(result.Items, result.Page, result.PageSize, result.Total);

    public static SqlValidationResponse Response(SqlValidationResult result) =>
        new(SqlAuditCodes.ToCode(result.Status), result.By, result.At);

    public static SqlItemSearch ItemSearch(
        string? kind, string? risk, string? validation, string? database, string? account, string? objectType, string? search, string? sort, int page, int pageSize)
    {
        var validationFilter = validation switch
        {
            "pending" => SqlItemValidationFilter.Pending,
            "valid" => SqlItemValidationFilter.Valid,
            "removed" => SqlItemValidationFilter.Removed,
            _ => SqlItemValidationFilter.Current,
        };
        var sortBy = sort?.TrimStart('-') switch
        {
            "account" => SqlItemSort.Account,
            "securable" => SqlItemSort.Securable,
            "permission" => SqlItemSort.Permission,
            "firstSeenAt" => SqlItemSort.FirstSeenAt,
            _ => SqlItemSort.Risk,
        };

        // Without an explicit order the riskiest elements come first.
        var descending = sort is null || sort.StartsWith('-');
        return new SqlItemSearch(kind, risk, validationFilter, database, account, objectType, search, sortBy, descending, PagingDefaults.Page(page, pageSize));
    }
}

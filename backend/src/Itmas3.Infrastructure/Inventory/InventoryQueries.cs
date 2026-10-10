using Itmas3.Application.Common;
using Itmas3.Application.Inventory;
using Itmas3.Application.Inventory.Queries;
using Itmas3.Domain.Inventory;
using Itmas3.Infrastructure.Authorization;
using Itmas3.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itmas3.Infrastructure.Inventory;

/// <summary>
/// Read-only projections of the device inventory (no tracking, parameterised SQL only). The collaborator name comes from
/// the ITMas user whose e-mail matches the owner e-mail, ignoring case; with several, the most recent sign-in wins (RF-4b).
/// </summary>
internal sealed class InventoryQueries(AppDbContext db) : IInventoryQueries
{
    public async Task<PagedResult<DeviceListItem>> SearchDevicesAsync(DeviceSearch search, CancellationToken cancellationToken)
    {
        var devices = db.Devices.AsNoTracking();

        devices = search.Status switch
        {
            DeviceStatusFilter.Pending => devices.Where(device => device.Status == DeviceStatus.Pending),
            DeviceStatusFilter.Active => devices.Where(device => device.Status == DeviceStatus.Active),
            DeviceStatusFilter.Deleted => devices.Where(device => device.Status == DeviceStatus.Deleted),
            DeviceStatusFilter.All => devices,
            _ => devices.Where(device => device.Status != DeviceStatus.Deleted),
        };

        if (DeviceTypes.Parse(search.Type) is { } type)
        {
            devices = devices.Where(device => device.Type == type);
        }

        if (LikePattern.Contains(search.Text) is { } pattern)
        {
            devices = devices.Where(device => EF.Functions.ILike(device.Name, pattern, LikePattern.Escape)
                || (device.Hostname != null && EF.Functions.ILike(device.Hostname, pattern, LikePattern.Escape))
                || (device.OwnerEmail != null && EF.Functions.ILike(device.OwnerEmail, pattern, LikePattern.Escape))
                || (device.OwnerEmail != null && db.Users.Any(user => user.Email != null
                    && user.Email.ToLower() == device.OwnerEmail
                    && EF.Functions.ILike(user.DisplayName, pattern, LikePattern.Escape))));
        }

        var rows = WithOwner(devices).Select(row => new
        {
            row.Device.Id,
            row.Device.Name,
            row.Device.Type,
            row.Device.Status,
            row.Device.Hostname,
            row.Device.OwnerEmail,
            row.OwnerName,
            row.Device.LastCollectedAt,
            ReportCount = db.InventoryReports.Count(report => report.DeviceId == row.Device.Id),
        });

        var total = await rows.CountAsync(cancellationToken);
        var ordered = (search.Sort, search.Descending) switch
        {
            (DeviceSort.Owner, false) => rows.OrderBy(row => row.OwnerName ?? row.OwnerEmail).ThenBy(row => row.Name),
            (DeviceSort.Owner, true) => rows.OrderByDescending(row => row.OwnerName ?? row.OwnerEmail).ThenBy(row => row.Name),
            (DeviceSort.LastCollectedAt, false) => rows.OrderBy(row => row.LastCollectedAt).ThenBy(row => row.Name),
            (DeviceSort.LastCollectedAt, true) => rows.OrderByDescending(row => row.LastCollectedAt != null).ThenByDescending(row => row.LastCollectedAt).ThenBy(row => row.Name),
            (_, true) => rows.OrderByDescending(row => row.Name),
            _ => rows.OrderBy(row => row.Name),
        };

        var page = await ordered.ThenBy(row => row.Id)
            .Skip(search.Page.Skip).Take(search.Page.PageSize)
            .ToListAsync(cancellationToken);

        var items = page
            .Select(row => new DeviceListItem(
                row.Id,
                row.Name,
                DeviceTypes.ToCode(row.Type),
                DeviceStatuses.ToCode(row.Status),
                row.Hostname,
                Owner(row.OwnerEmail, row.OwnerName),
                row.LastCollectedAt,
                row.ReportCount))
            .ToList();

        return new PagedResult<DeviceListItem>(items, search.Page.Page, search.Page.PageSize, total);
    }

    public async Task<DeviceDetail?> GetDeviceAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await WithOwner(db.Devices.AsNoTracking().Where(device => device.Id == id))
            .Select(candidate => new
            {
                candidate.Device,
                candidate.OwnerName,
                ReportCount = db.InventoryReports.Count(report => report.DeviceId == candidate.Device.Id),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var device = row.Device;
        return new DeviceDetail(
            device.Id,
            device.Name,
            DeviceTypes.ToCode(device.Type),
            DeviceStatuses.ToCode(device.Status),
            device.Revision,
            Owner(device.OwnerEmail, row.OwnerName),
            device.Hostname,
            device.OsName,
            device.OsVersion,
            device.CreatedAt,
            device.EnrolledAt,
            device.EnrollmentCodeIssuedAt,
            device.EnrollmentCodeHash is not null,
            device.LastSeenAt,
            device.LastCollectedAt,
            device.DeletedAt,
            row.ReportCount);
    }

    public async Task<PagedResult<DeviceReportListItem>?> GetDeviceReportsAsync(Guid deviceId, PageRequest page, CancellationToken cancellationToken)
    {
        var device = await db.Devices.AsNoTracking()
            .Where(candidate => candidate.Id == deviceId)
            .Select(candidate => new { candidate.LastReportId })
            .SingleOrDefaultAsync(cancellationToken);
        if (device is null)
        {
            return null;
        }

        var reports = db.InventoryReports.AsNoTracking().Where(report => report.DeviceId == deviceId);
        var total = await reports.CountAsync(cancellationToken);
        var items = await reports
            .OrderByDescending(report => report.CollectedAt).ThenByDescending(report => report.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(report => new DeviceReportListItem(
                report.Id,
                report.CollectedAt,
                report.ReceivedAt,
                report.ConnectionIp,
                report.PublicIp,
                report.CountryCode,
                report.Region,
                report.City,
                report.AgentVersion,
                report.UnavailableCount,
                report.Id == device.LastReportId))
            .ToListAsync(cancellationToken);

        return new PagedResult<DeviceReportListItem>(items, page.Page, page.PageSize, total);
    }

    public async Task<DeviceReportDetail?> GetDeviceReportAsync(Guid deviceId, Guid? reportId, CancellationToken cancellationToken)
    {
        var device = await WithOwner(db.Devices.AsNoTracking().Where(candidate => candidate.Id == deviceId))
            .Select(candidate => new
            {
                candidate.Device.Id,
                candidate.Device.Name,
                candidate.Device.Type,
                candidate.Device.Status,
                candidate.Device.OwnerEmail,
                candidate.OwnerName,
                candidate.Device.LastReportId,
            })
            .SingleOrDefaultAsync(cancellationToken);

        var wanted = reportId ?? device?.LastReportId;
        if (device is null || wanted is null)
        {
            return null;
        }

        var report = await db.InventoryReports.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == wanted && candidate.DeviceId == deviceId, cancellationToken);
        if (report is null)
        {
            return null;
        }

        var header = new DeviceHeader(
            device.Id, device.Name, DeviceTypes.ToCode(device.Type), DeviceStatuses.ToCode(device.Status), Owner(device.OwnerEmail, device.OwnerName));
        return new DeviceReportDetail(
            report.Id, header, report.CollectedAt, report.ReceivedAt, report.ConnectionIp, report.Id == device.LastReportId, report.Payload);
    }

    private static DeviceOwner? Owner(string? email, string? name) => email is null ? null : new DeviceOwner(email, name);

    /// <summary>Adds the collaborator name with a correlated subquery (owner e-mails are stored lowercase).</summary>
    private IQueryable<DeviceWithOwner> WithOwner(IQueryable<Device> devices) =>
        devices.Select(device => new DeviceWithOwner
        {
            Device = device,
            OwnerName = device.OwnerEmail == null
                ? null
                : db.Users
                    .Where(user => user.Email != null && user.Email.ToLower() == device.OwnerEmail)
                    .OrderByDescending(user => user.LastSignInAt != null).ThenByDescending(user => user.LastSignInAt)
                    .Select(user => user.DisplayName)
                    .FirstOrDefault(),
        });

    private sealed class DeviceWithOwner
    {
        public required Device Device { get; init; }

        public string? OwnerName { get; init; }
    }
}

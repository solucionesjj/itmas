using Itmas3.Application.Inventory.Abstractions;

namespace Itmas3.Application.Inventory.PurgeHistory;

/// <summary>Use case: delete inventory history older than the retention period, keeping each device's latest (RF-17).</summary>
public sealed class PurgeInventoryHistoryHandler(
    IInventoryReportRepository reports,
    InventorySettings settings,
    TimeProvider timeProvider)
{
    public Task<int> HandleAsync(CancellationToken cancellationToken) =>
        reports.DeleteCollectedBeforeAsync(timeProvider.GetUtcNow().AddDays(-settings.HistoryRetentionDays), cancellationToken);
}

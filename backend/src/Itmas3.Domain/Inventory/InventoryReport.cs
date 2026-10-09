using Itmas3.Domain.Common;

namespace Itmas3.Domain.Inventory;

/// <summary>
/// One inventory sent by a device (SPEC-0002). Stores the full validated payload plus the two IPs of RF-16:
/// the public IP declared by the agent (from ipinfo.io) and the connection IP observed by the server.
/// </summary>
public sealed class InventoryReport : Entity<Guid>
{
    private InventoryReport(Guid id, Guid deviceId, DateTimeOffset collectedAt, DateTimeOffset receivedAt, string payload)
        : base(id)
    {
        DeviceId = deviceId;
        CollectedAt = collectedAt;
        ReceivedAt = receivedAt;
        Payload = payload;
    }

    public Guid DeviceId { get; private set; }

    public DateTimeOffset CollectedAt { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>Source IP of the HTTP request as seen by the server (cannot be set by the agent).</summary>
    public string? ConnectionIp { get; private set; }

    /// <summary>Public IP declared by the agent (ipinfo.io). Device-declared: compare with <see cref="ConnectionIp"/>.</summary>
    public string? PublicIp { get; private set; }

    public string? CountryCode { get; private set; }

    public string? Region { get; private set; }

    public string? City { get; private set; }

    /// <summary>Validated inventory document (JSON, contract v1).</summary>
    public string Payload { get; private set; }

    public static InventoryReport Receive(
        Guid deviceId,
        DateTimeOffset collectedAt,
        DateTimeOffset receivedAt,
        string? connectionIp,
        DeclaredPublicNetwork? publicNetwork,
        string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new DomainException("El inventario está vacío.");
        }

        return new InventoryReport(Guid.CreateVersion7(receivedAt), deviceId, collectedAt, receivedAt, payload)
        {
            ConnectionIp = connectionIp,
            PublicIp = publicNetwork?.Ip,
            CountryCode = publicNetwork?.CountryCode,
            Region = publicNetwork?.Region,
            City = publicNetwork?.City,
        };
    }
}

/// <summary>Public IP and location declared by the agent (ADR-0011).</summary>
public sealed record DeclaredPublicNetwork(string Ip, string CountryCode, string? Region, string? City);

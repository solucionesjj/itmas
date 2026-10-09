using Itmas3.Application.Inventory.Abstractions;

namespace Itmas3.Application.Inventory.AuthenticateDevice;

/// <summary>Resolves the device that owns an API key (ADR-0009). Lookup by hash: the key is never stored.</summary>
public sealed class DeviceAuthenticator(IDeviceRepository devices)
{
    public const int MaxApiKeyLength = 128;

    public async Task<Guid?> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length > MaxApiKeyLength)
        {
            return null;
        }

        return await devices.FindIdByApiKeyHashAsync(DeviceSecrets.Hash(apiKey), cancellationToken);
    }
}

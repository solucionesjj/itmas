using Itmas3.Application.Inventory.Abstractions;

namespace Itmas3.Application.Inventory.AuthenticateDevice;

/// <summary>
/// Resolves the active device that owns an API key (ADR-0009). Lookup by hash: the key is never stored.
/// Deleted devices never authenticate (SPEC-0003 RN-6).
/// </summary>
public sealed class DeviceAuthenticator(IDeviceRepository devices)
{
    public const int MaxApiKeyLength = 128;

    public async Task<Guid?> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length > MaxApiKeyLength)
        {
            return null;
        }

        return await devices.FindActiveIdByApiKeyHashAsync(DeviceSecrets.Hash(apiKey), cancellationToken);
    }
}

/// <summary>
/// Resolves the device that owns a one-time enrollment code (ADR-0012). Malformed codes are rejected without a lookup;
/// consumed, replaced or deleted devices' codes no longer resolve.
/// </summary>
public sealed class EnrollmentCodeAuthenticator(IDeviceRepository devices)
{
    public async Task<Guid?> AuthenticateAsync(string code, CancellationToken cancellationToken) =>
        DeviceSecrets.HashEnrollmentCode(code) is { } hash
            ? await devices.FindIdByEnrollmentCodeHashAsync(hash, cancellationToken)
            : null;
}

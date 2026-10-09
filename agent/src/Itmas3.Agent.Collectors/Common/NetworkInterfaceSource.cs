using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Itmas3.Agent.Collectors.Model;

namespace Itmas3.Agent.Collectors.Common;

public interface INetworkInterfaceSource
{
    IReadOnlyList<NetworkInterfaceInfo> GetInterfaces();
}

/// <summary>
/// Local IPs of active interfaces on every OS (SPEC-0002 §8.3.5): loopback and link-local addresses are excluded;
/// virtual interfaces (Docker, Hyper-V) are kept with their name.
/// </summary>
public sealed class NetworkInterfaceSource : INetworkInterfaceSource
{
    public IReadOnlyList<NetworkInterfaceInfo> GetInterfaces() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(nic => Describe(nic.Name, nic.GetIPProperties().UnicastAddresses.Select(address => address.Address)))
            .OfType<NetworkInterfaceInfo>()
            .Take(64)
            .ToList();

    /// <summary>Builds the description of one interface; null when it has no reportable address.</summary>
    public static NetworkInterfaceInfo? Describe(string name, IEnumerable<IPAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var reportable = addresses.Where(IsReportable).ToList();
        if (reportable.Count == 0 || TextSanitizer.Clean(name) is not { } cleanName)
        {
            return null;
        }

        return new NetworkInterfaceInfo(
            cleanName,
            reportable.Where(address => address.AddressFamily == AddressFamily.InterNetwork).Select(Format).Distinct().Take(64).ToList(),
            reportable.Where(address => address.AddressFamily == AddressFamily.InterNetworkV6).Select(Format).Distinct().Take(64).ToList());
    }

    public static bool IsReportable(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => !(address.GetAddressBytes() is [169, 254, ..]),
            AddressFamily.InterNetworkV6 => !address.IsIPv6LinkLocal && !address.IsIPv6Multicast,
            _ => false,
        };
    }

    // Scope ids (fe80::1%en0) are meaningless outside the machine.
    private static string Format(IPAddress address) => new IPAddress(address.GetAddressBytes()).ToString();
}

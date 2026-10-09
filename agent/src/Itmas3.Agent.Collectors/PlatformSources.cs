using Itmas3.Agent.Collectors.Common;
using Itmas3.Agent.Collectors.Linux;
using Itmas3.Agent.Collectors.MacOs;
using Itmas3.Agent.Collectors.Processes;
using Itmas3.Agent.Collectors.Windows;

namespace Itmas3.Agent.Collectors;

public static class PlatformSources
{
    /// <summary>Inventory source of the running operating system.</summary>
    public static IPlatformInventorySource ForCurrentPlatform(IProcessRunner processes, ISystemFiles files, TimeProvider timeProvider)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsInventorySource();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxInventorySource(processes, files, timeProvider);
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOsInventorySource(processes, files);
        }

        throw new PlatformNotSupportedException("The ITMas agent supports Windows, Linux and macOS.");
    }
}

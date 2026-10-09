using System.Security.Principal;

namespace Itmas3.Agent.Platform;

public static class Elevation
{
    /// <summary>True when running as Administrator (Windows) or root (Linux, macOS).</summary>
    public static bool IsElevated()
    {
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        return Environment.IsPrivilegedProcess;
    }
}

using System.Runtime.InteropServices;

namespace Itmas3.Agent.Collectors.Common;

public static class RuntimeArchitecture
{
    public static string Current => RuntimeInformation.OSArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.Arm64 => "arm64",
        Architecture.X86 => "x86",
        Architecture.Arm => "arm",
        _ => "unknown",
    };
}

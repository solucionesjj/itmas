using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Itmas3.Agent.Configuration;

/// <summary>
/// Files readable only by administrators (ADR-0010 §6): 0700/0600 on Linux and macOS; ACL restricted to SYSTEM and
/// Administrators on Windows, where the API key is additionally encrypted with DPAPI (machine scope).
/// </summary>
public static class SecureFiles
{
    private static readonly byte[] DpapiEntropy = Encoding.UTF8.GetBytes("itmas3-agent-credential-v1");

    public static void EnsureDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            EnsureWindowsDirectory(path);
            return;
        }

        Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>Writes atomically (temporary file + move) with owner-only permissions.</summary>
    public static void WriteText(string path, string content) => WriteBytes(path, Encoding.UTF8.GetBytes(content));

    public static void WriteSecret(string path, string secret)
    {
        var bytes = Encoding.UTF8.GetBytes(secret);
        WriteBytes(path, OperatingSystem.IsWindows() ? ProtectedData.Protect(bytes, DpapiEntropy, DataProtectionScope.LocalMachine) : bytes);
    }

    public static string? ReadSecret(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(path);
        return Encoding.UTF8.GetString(OperatingSystem.IsWindows() ? ProtectedData.Unprotect(bytes, DpapiEntropy, DataProtectionScope.LocalMachine) : bytes).Trim();
    }

    private static void WriteBytes(string path, byte[] content)
    {
        var temporary = path + ".tmp";
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var stream = new FileStream(temporary, options))
        {
            stream.Write(content);
            stream.Flush(flushToDisk: true);
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.Move(temporary, path, overwrite: true);
    }

    [SupportedOSPlatform("windows")]
    private static void EnsureWindowsDirectory(string path)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
        {
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(sid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        var directory = Directory.CreateDirectory(path);
        directory.SetAccessControl(security);
    }
}

namespace Itmas3.Agent.Collectors.Common;

/// <summary>Read-only access to system files and mounts (abstracted for tests).</summary>
public interface ISystemFiles
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken);

    IEnumerable<string> EnumerateDirectories(string path);

    /// <summary>Total and available bytes of the file system mounted at <paramref name="mountPoint"/>, or null.</summary>
    (long Total, long Free)? GetSpace(string mountPoint);
}

public sealed class SystemFiles : ISystemFiles
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) => File.ReadAllTextAsync(path, cancellationToken);

    public IEnumerable<string> EnumerateDirectories(string path) =>
        Directory.EnumerateDirectories(path, "*", new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = true });

    public (long Total, long Free)? GetSpace(string mountPoint)
    {
        try
        {
            var drive = new DriveInfo(mountPoint);
            return drive.IsReady ? (drive.TotalSize, drive.AvailableFreeSpace) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}

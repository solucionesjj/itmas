using System.Diagnostics;
using System.Text;

namespace Itmas3.Agent.Collectors.Processes;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Runs operating-system commands (constitution §13.4). Abstracted so collectors can be tested with fixtures.</summary>
public interface IProcessRunner
{
    /// <summary>True when the executable exists at its absolute path.</summary>
    bool Exists(string executablePath);

    Task<ProcessResult> RunAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

/// <summary>
/// Safe process execution: absolute path only, no shell, fixed arguments passed as an argument list (never a command
/// line), <c>LC_ALL=C</c> for stable output, bounded output and a timeout after which the process tree is killed.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private const int MaxOutputChars = 8 * 1024 * 1024;

    public bool Exists(string executablePath) => Path.IsPathFullyQualified(executablePath) && File.Exists(executablePath);

    public async Task<ProcessResult> RunAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException("Only absolute executable paths are allowed.", nameof(executablePath));
        }

        if (!File.Exists(executablePath))
        {
            throw new CollectorException(Model.UnavailableReasons.NotSupported, $"{executablePath} is not available.");
        }

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["LC_ALL"] = "C";
        startInfo.Environment["LANG"] = "C";

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.StandardInput.Close();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        var output = ReadBoundedAsync(process.StandardOutput, timeout.Token);
        var error = ReadBoundedAsync(process.StandardError, timeout.Token);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return new ProcessResult(process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            throw new CollectorException(Model.UnavailableReasons.Timeout, $"{Path.GetFileName(executablePath)} timed out.");
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new char[8192];
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (builder.Length + read > MaxOutputChars)
            {
                throw new CollectorException(Model.UnavailableReasons.Error, "Command output exceeds the allowed size.");
            }

            builder.Append(buffer, 0, read);
        }

        return builder.ToString();
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }
}

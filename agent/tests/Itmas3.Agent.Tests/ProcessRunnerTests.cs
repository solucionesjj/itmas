using Itmas3.Agent.Collectors;
using Itmas3.Agent.Collectors.Model;
using Itmas3.Agent.Collectors.Processes;

namespace Itmas3.Agent.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task Relative_paths_are_rejected() =>
        await Assert.ThrowsAsync<ArgumentException>(() => new ProcessRunner().RunAsync("ls", [], Fixtures.Ct));

    [Fact]
    public async Task Missing_tools_are_not_supported()
    {
        var path = OperatingSystem.IsWindows() ? @"C:\nonexistent\tool.exe" : "/nonexistent/tool";

        var exception = await Assert.ThrowsAsync<CollectorException>(() => new ProcessRunner().RunAsync(path, [], Fixtures.Ct));
        Assert.Equal(UnavailableReasons.NotSupported, exception.Reason);
    }

    [Fact]
    public async Task Arguments_are_not_interpreted_by_a_shell()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix tools only.");

        var result = await new ProcessRunner().RunAsync("/bin/echo", ["$(id)", "; ls", "`whoami`"], Fixtures.Ct);

        Assert.Equal("$(id) ; ls `whoami`", result.StandardOutput.Trim());
    }
}

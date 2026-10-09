namespace Itmas3.Agent.Tests;

internal static class Fixtures
{
    public static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", relativePath));

    public static CancellationToken Ct => TestContext.Current.CancellationToken;
}

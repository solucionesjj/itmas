using System.Text;

namespace Itmas3.Agent.Cli;

/// <summary>Console interaction (abstracted for tests).</summary>
public interface IAgentConsole
{
    /// <summary>False when input is redirected (scripts): no questions are asked then.</summary>
    bool IsInteractive { get; }

    void WriteLine(string text = "");

    void WriteError(string text);

    string? ReadLine();

    /// <summary>Reads a line without echoing it (the authorization code is never shown, CA-3).</summary>
    string? ReadSecret();
}

public sealed class AgentConsole : IAgentConsole
{
    public bool IsInteractive => !Console.IsInputRedirected;

    public void WriteLine(string text = "") => Console.Out.WriteLine(text);

    public void WriteError(string text) => Console.Error.WriteLine(text);

    public string? ReadLine() => Console.ReadLine();

    public string? ReadSecret()
    {
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine();
        }

        var builder = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return builder.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (builder.Length > 0)
                {
                    builder.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                builder.Append(key.KeyChar);
            }
        }
    }
}

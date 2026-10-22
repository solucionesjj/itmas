namespace Itmas3.Infrastructure.Agenda.Graph;

/// <summary>
/// What the Graph adapter needs from the deployment configuration (SPEC-0006 §12.3, ADR-0016). <see cref="ClientSecret"/> is a
/// SECRET: it is never part of <see cref="ToString"/>, of a log or of a message.
/// </summary>
public sealed class GraphOutlookSettings
{
    /// <summary>Longest time a call to Graph (token included) may take (RNF-5).</summary>
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(10);

    public GraphOutlookSettings(string tenantId, string clientId, string clientSecret, string mailbox)
    {
        TenantId = Required(tenantId, nameof(tenantId));
        ClientId = Required(clientId, nameof(clientId));
        ClientSecret = Required(clientSecret, nameof(clientSecret));
        Mailbox = Required(mailbox, nameof(mailbox));
    }

    public string TenantId { get; }

    public string ClientId { get; }

    /// <summary>SECRET. Credential of the Entra ID application of Graph (ADR-0016 §3).</summary>
    public string ClientSecret { get; }

    /// <summary>Address of the shared mailbox whose default calendar receives the events (ADR-0016 §1).</summary>
    public string Mailbox { get; }

    public TimeSpan RequestTimeout { get; init; } = DefaultRequestTimeout;

    public override string ToString() => nameof(GraphOutlookSettings);

    // The message names the parameter, never its value.
    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", name) : value.Trim();
}

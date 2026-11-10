namespace Itmas3.Infrastructure.Agenda.Smtp;

/// <summary>
/// What the SMTP adapter needs (SPEC-0008, ADR-0023 decisions 1 and 3). The host and the port are fixed in production
/// (<see cref="ProductionHost"/>:<see cref="ProductionPort"/>): there is no setting for them, so a typo or a look-alike domain can never
/// receive the password. Only the integration test builds other values, through the internal constructor.
/// <para>
/// <see cref="Password"/> is a SECRET: this is not a record and <see cref="ToString"/> never prints it.
/// </para>
/// </summary>
public sealed class SmtpOutlookSettings
{
    public const string ProductionHost = "smtp.office365.com";
    public const int ProductionPort = 587;

    public SmtpOutlookSettings(string username, string password)
        : this(username, password, ProductionHost, ProductionPort)
    {
    }

    internal SmtpOutlookSettings(string username, string password, string host, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        Username = username.Trim().ToLowerInvariant();
        Password = password;
        Host = host;
        Port = port;
    }

    /// <summary>The account that authenticates, sends and organizes the meetings.</summary>
    public string Username { get; }

    /// <summary>SECRET. Only in the environment of the <c>api</c> container (<c>AGENDA_SMTP_PASSWORD</c>).</summary>
    internal string Password { get; }

    public string Host { get; }

    public int Port { get; }

    public override string ToString() => $"SmtpOutlookSettings {{ Host = {Host}, Port = {Port} }}";
}

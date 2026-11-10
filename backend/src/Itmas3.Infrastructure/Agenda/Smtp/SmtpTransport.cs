using System.Net.Security;
using System.Security.Authentication;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Itmas3.Infrastructure.Agenda.Smtp;

/// <summary>
/// One SMTP connection, as the adapter uses it: connect with TLS and authenticate, send, close. It exists so that the adapter can be
/// tested with a double (ADR-0023 decision 13) without a server; the real one is <see cref="MailKitSmtpTransport"/>.
/// </summary>
internal interface ISmtpTransport : IAsyncDisposable
{
    bool IsConnected { get; }

    /// <summary>Connects with STARTTLS (never in clear) and authenticates. Throws the exceptions of MailKit on failure.</summary>
    Task ConnectAsync(SmtpOutlookSettings settings, CancellationToken cancellationToken);

    /// <summary>Sends the message. Returns the recipients the server refused for good at <c>RCPT</c>; the others got it.</summary>
    Task<IReadOnlyList<string>> SendAsync(MimeMessage message, CancellationToken cancellationToken);
}

internal interface ISmtpTransportFactory
{
    ISmtpTransport Create();
}

/// <summary>
/// Creates <see cref="MailKitSmtpTransport"/>s. In production the certificate is validated strictly: the chain, the name and the
/// revocation must be valid, with no exception (RNF-1). Only the integration test passes <paramref name="testTrust"/>, to trust the CA of
/// its test server; there is no setting that does it.
/// </summary>
internal sealed class MailKitSmtpTransportFactory(RemoteCertificateValidationCallback? testTrust = null) : ISmtpTransportFactory
{
    /// <summary>Whether this factory trusts something else than the system (only in tests).</summary>
    internal bool UsesTestTrust => testTrust is not null;

    public ISmtpTransport Create() => new MailKitSmtpTransport(testTrust ?? Strict);

    /// <summary>Accepts the certificate only when the platform found no error at all: chain, name and revocation.</summary>
    internal static bool Strict(object sender, System.Security.Cryptography.X509Certificates.X509Certificate? certificate,
        System.Security.Cryptography.X509Certificates.X509Chain? chain, SslPolicyErrors errors) => errors == SslPolicyErrors.None;
}

/// <summary>
/// The MailKit connection (ADR-0023 decisions 1 and 8): STARTTLS only, TLS 1.2 or 1.3, certificate revocation checked, strict validation,
/// authentication only after TLS. No protocol logger is attached, so neither the dialog nor the <c>AUTH</c> line is written anywhere.
/// A recipient refused for good does not stop the message for the others.
/// </summary>
internal sealed class MailKitSmtpTransport(RemoteCertificateValidationCallback validation) : ISmtpTransport
{
    /// <summary>
    /// Socket timeout of MailKit, above the 15 s of the adapter so that its own timeout (<c>smtp_timeout</c>) always fires first.
    /// </summary>
    private const int TimeoutMilliseconds = 20_000;

    private RecordingSmtpClient? _client;

    public bool IsConnected => _client is { IsConnected: true, IsAuthenticated: true };

    public async Task ConnectAsync(SmtpOutlookSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var client = new RecordingSmtpClient
        {
            Timeout = TimeoutMilliseconds,
            CheckCertificateRevocation = true,
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            ServerCertificateValidationCallback = validation,
        };
        _client = client;

        await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.StartTls, cancellationToken);
        if (!client.IsSecure)
        {
            // StartTls already refuses a server without STARTTLS; this makes sure nothing is ever authenticated in clear.
            throw new SslHandshakeException("The SMTP connection is not encrypted.");
        }

        await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var client = _client ?? throw new InvalidOperationException("Not connected.");
        client.Rejected.Clear();
        try
        {
            await client.SendAsync(message, cancellationToken);
        }
        catch (NoRecipientsAcceptedException)
        {
            // Every recipient was refused for good: the message reached nobody. The session is left in the middle of a transaction,
            // so it is dropped and the next message connects again.
            await DisposeAsync();
        }

        return [.. client.Rejected];
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is { } client)
        {
            _client = null;
            try
            {
                if (client.IsConnected)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await client.DisconnectAsync(quit: true, timeout.Token);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // Closing is best effort; the detail of the server is never logged.
            }
            finally
            {
                client.Dispose();
            }
        }
    }

    /// <summary>Raised when the server refused every recipient for good, so that the caller learns who without failing the batch.</summary>
    private sealed class NoRecipientsAcceptedException : Exception
    {
        public NoRecipientsAcceptedException()
            : base("No recipient was accepted.")
        {
        }

        public NoRecipientsAcceptedException(string message)
            : base(message)
        {
        }

        public NoRecipientsAcceptedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Keeps the recipients refused for good instead of failing the message for everyone. As MailKit asks when
    /// <see cref="OnRecipientNotAccepted"/> does not throw, <see cref="OnNoRecipientsAccepted"/> throws instead, so that a message with
    /// no accepted recipient is never sent.
    /// </summary>
    private sealed class RecordingSmtpClient : SmtpClient
    {
        public List<string> Rejected { get; } = [];

        protected override void OnNoRecipientsAccepted(MimeMessage message) => throw new NoRecipientsAcceptedException();

        protected override void OnRecipientNotAccepted(MimeMessage message, MailboxAddress mailbox, SmtpResponse response)
        {
            if ((int)response.StatusCode is >= 500 and < 600)
            {
                Rejected.Add(mailbox.Address.ToLowerInvariant());
                return;
            }

            // A temporary refusal (4xx) is a limit of the server: the message fails and the batch stops.
            base.OnRecipientNotAccepted(message, mailbox, response);
        }
    }
}

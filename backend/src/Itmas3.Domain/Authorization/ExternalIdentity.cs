namespace Itmas3.Domain.Authorization;

/// <summary>
/// Link between an application user and an identity-provider account. Keyed by immutable identifiers
/// (provider, issuer/tenant, subject/object id), never by e-mail.
/// </summary>
public sealed class ExternalIdentity
{
    internal ExternalIdentity(string provider, string issuer, string subject)
    {
        Provider = provider;
        Issuer = issuer;
        Subject = subject;
    }

    public string Provider { get; private set; }

    public string Issuer { get; private set; }

    public string Subject { get; private set; }
}

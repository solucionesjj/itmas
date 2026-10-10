namespace Itmas3.Application.Common.Exceptions;

/// <summary>
/// A credential that authenticated the request is no longer valid when the use case runs (e.g. a one-time enrollment
/// code consumed by a concurrent request). HTTP 401 without details, so the reason is not disclosed.
/// </summary>
public sealed class CredentialRejectedException : Exception
{
    public CredentialRejectedException()
        : base("The credential is not valid.")
    {
    }

    public CredentialRejectedException(string message)
        : base(message)
    {
    }

    public CredentialRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

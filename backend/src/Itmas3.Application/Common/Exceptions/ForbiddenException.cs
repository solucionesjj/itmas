namespace Itmas3.Application.Common.Exceptions;

/// <summary>
/// The caller is authenticated and holds the endpoint permission, but a business safeguard forbids this specific
/// operation (e.g. privilege escalation). Mapped to HTTP 403; the message is written to be client-safe.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message)
        : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

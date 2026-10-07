namespace Itmas3.Domain.Common;

/// <summary>
/// Raised when a business rule is violated. The message MUST be safe to show to API clients:
/// it is returned in the Problem Details response (HTTP 422).
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

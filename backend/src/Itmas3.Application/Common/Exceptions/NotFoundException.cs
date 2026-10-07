namespace Itmas3.Application.Common.Exceptions;

/// <summary>
/// Raised by a use case when the requested resource does not exist (or is not visible to the caller).
/// Mapped to HTTP 404. The message is NOT returned to the client.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

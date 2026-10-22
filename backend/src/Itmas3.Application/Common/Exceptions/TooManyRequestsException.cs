namespace Itmas3.Application.Common.Exceptions;

/// <summary>
/// A shared budget of the operation is spent (for example, the notices of the agenda, SPEC-0006 P-26). HTTP 429 with
/// <c>Retry-After</c> when <see cref="RetryAfter"/> is known; client-safe message.
/// </summary>
public sealed class TooManyRequestsException : Exception
{
    public TooManyRequestsException(string message)
        : base(message)
    {
    }

    public TooManyRequestsException(string message, TimeSpan? retryAfter)
        : base(message)
    {
        RetryAfter = retryAfter;
    }

    public TooManyRequestsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public TimeSpan? RetryAfter { get; }
}

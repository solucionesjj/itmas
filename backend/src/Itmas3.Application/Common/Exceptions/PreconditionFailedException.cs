namespace Itmas3.Application.Common.Exceptions;

/// <summary>The resource changed since the client read it (optimistic concurrency). HTTP 412; client-safe message.</summary>
public sealed class PreconditionFailedException : Exception
{
    public PreconditionFailedException(string message)
        : base(message)
    {
    }

    public PreconditionFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

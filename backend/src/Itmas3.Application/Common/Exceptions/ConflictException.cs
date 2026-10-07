namespace Itmas3.Application.Common.Exceptions;

/// <summary>The request conflicts with the current state (duplicate name, existing dependencies). HTTP 409; client-safe message.</summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

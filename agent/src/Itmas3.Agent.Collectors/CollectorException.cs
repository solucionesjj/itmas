namespace Itmas3.Agent.Collectors;

/// <summary>A piece of the inventory could not be obtained. <see cref="Reason"/> is a contract value (UnavailableReasons).</summary>
public sealed class CollectorException : Exception
{
    public CollectorException(string reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public CollectorException(string reason, string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = reason;
    }

    public string Reason { get; }
}

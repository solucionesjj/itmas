namespace Itmas3.Infrastructure.HealthChecks;

public static class HealthCheckTags
{
    /// <summary>Dependencies required to serve traffic (used by <c>/health/ready</c>).</summary>
    public const string Ready = "ready";
}

namespace Itmas3.IntegrationTests.Infrastructure;

/// <summary>
/// The tests that measure response times against a budget run alone, after the parallel ones: measured while other classes
/// start hosts and seed data, they fail by load and not by the code (SPEC-0005 RNF-2 already did once).
/// </summary>
[CollectionDefinition(nameof(PerformanceCollection), DisableParallelization = true)]
public sealed class PerformanceCollection;

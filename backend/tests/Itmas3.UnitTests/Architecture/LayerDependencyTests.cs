using System.Reflection;

namespace Itmas3.UnitTests.Architecture;

/// <summary>Enforces the Clean Architecture dependency rule (constitution §2): dependencies point inwards only.</summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(Itmas3.Domain.Common.DomainException).Assembly;
    private static readonly Assembly Application = typeof(Itmas3.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Itmas3.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_any_other_layer_or_framework_package()
    {
        var references = ReferencedAssemblyNames(Domain);

        Assert.DoesNotContain(references, name => name.StartsWith("Itmas3.", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_depends_only_on_Domain()
    {
        var references = ReferencedAssemblyNames(Application);

        Assert.DoesNotContain(Infrastructure.GetName().Name, references);
        Assert.DoesNotContain(Api.GetName().Name, references);
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Npgsql", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        Assert.DoesNotContain(Api.GetName().Name, ReferencedAssemblyNames(Infrastructure));
    }

    private static string[] ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!).ToArray();
}

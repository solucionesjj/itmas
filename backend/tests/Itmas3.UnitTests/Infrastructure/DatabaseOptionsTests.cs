using Itmas3.Infrastructure.Persistence;
using Npgsql;

namespace Itmas3.UnitTests.Infrastructure;

public sealed class DatabaseOptionsTests
{
    private static DatabaseOptions Options() => new()
    {
        Host = "postgres",
        Port = 5432,
        Name = "itmas3",
        Username = "itmas3_app",
        Password = "not-a-real-password",
    };

    [Fact]
    public void ToString_never_includes_the_password()
    {
        Assert.DoesNotContain("not-a-real-password", Options().ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Connection_string_disables_server_error_detail()
    {
        var builder = new NpgsqlConnectionStringBuilder(Options().BuildConnectionString());

        Assert.False(builder.IncludeErrorDetail);
        Assert.Equal("postgres", builder.Host);
        Assert.Equal("itmas3", builder.Database);
    }
}

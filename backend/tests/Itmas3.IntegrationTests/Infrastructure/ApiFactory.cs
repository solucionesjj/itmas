using Itmas3.Domain.Authorization;
using Itmas3.Infrastructure.Persistence;
using Itmas3.Infrastructure.Persistence.Records;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itmas3.IntegrationTests.Infrastructure;

public sealed record TestUser(Guid Id, string SecurityStamp, string DisplayName);

/// <summary>
/// Hosts the real API in a non-Development environment (so production-only safeguards are active),
/// connected to the test PostgreSQL with the least-privilege runtime role.
/// Microsoft Entra ID values are syntactically valid placeholders: no request is ever sent to Entra.
/// </summary>
public sealed class ApiFactory(PostgreSqlFixture database) : WebApplicationFactory<Program>
{
    public const string TenantId = "11111111-1111-1111-1111-111111111111";

    /// <summary>Random per test host: never a real code (constitution §4.6).</summary>
    public string EnrollmentCode { get; } = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware", "Warning");

        builder.UseSetting("Database:Host", database.Host);
        builder.UseSetting("Database:Port", database.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Database:Name", PostgreSqlFixture.DatabaseName);
        builder.UseSetting("Database:Username", PostgreSqlFixture.AppUser);
        builder.UseSetting("Database:Password", database.AppPassword);
        builder.UseSetting("Database:SslMode", "Disable");

        builder.UseSetting("EntraId:TenantId", TenantId);
        builder.UseSetting("EntraId:ClientId", "22222222-2222-2222-2222-222222222222");
        builder.UseSetting("EntraId:ClientSecret", "placeholder-not-a-secret");

        // Generous limit: tests issue many requests from the same client.
        builder.UseSetting("RateLimiting:PermitLimitPerMinute", "10000");
        builder.UseSetting("RateLimiting:DeviceEnrollmentPermitLimitPerMinute", "1000");

        builder.UseSetting("Inventory:EnrollmentCode", EnrollmentCode);

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(TestEndpointsController).Assembly);

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName);
        });
    }

    public HttpClient CreateAnonymousClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>Client authenticated as <paramref name="user"/>, sending the anti-CSRF header.</summary>
    public HttpClient CreateClientFor(TestUser user)
    {
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, user.Id.ToString());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.StampHeader, user.SecurityStamp);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.NameHeader, user.DisplayName);
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        return client;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(bool isRoot = false, params string[] permissions) =>
        CreateClientFor(await CreateUserAsync(isRoot, permissions));

    /// <summary>
    /// Creates an application user directly in the database (as if provisioned at sign-in). When permissions are
    /// given, a dedicated policy with them is created and assigned.
    /// </summary>
    public async Task<TestUser> CreateUserAsync(bool isRoot = false, params string[] permissions)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var subject = Guid.NewGuid().ToString();
        var user = User.Provision("test", TenantId, subject, $"Usuario {subject[..8]}", $"{subject[..8]}@example.com", now);
        user.SetRoot(isRoot);

        if (permissions.Length > 0)
        {
            var definitions = await db.Permissions.Where(permission => permissions.Contains(permission.Id)).ToListAsync();
            if (definitions.Count != permissions.Distinct().Count())
            {
                throw new InvalidOperationException("Unknown permission requested by the test.");
            }

            var policy = Policy.Create($"Política de prueba {Guid.NewGuid():N}", string.Empty, definitions, now);
            db.Policies.Add(policy);
            user.AssignPolicy(policy.Id, null, now);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        await BumpAuthorizationVersionAsync(db);
        return new TestUser(user.Id, user.SecurityStamp, user.DisplayName);
    }

    /// <summary>Direct database changes made by tests must invalidate cached authorization snapshots, like the API does.</summary>
    public static Task BumpAuthorizationVersionAsync(AppDbContext db) =>
        db.AuthorizationState.Where(state => state.Id == AuthorizationState.SingletonId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(state => state.Version, state => state.Version + 1));
}

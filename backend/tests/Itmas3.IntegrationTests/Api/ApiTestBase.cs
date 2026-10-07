using System.Net.Http.Json;
using System.Text.Json;
using Itmas3.IntegrationTests.Infrastructure;

namespace Itmas3.IntegrationTests.Api;

public abstract class ApiTestBase(PostgreSqlFixture database) : IAsyncDisposable
{
    protected ApiFactory Factory { get; } = new(database);

    protected PostgreSqlFixture Database { get; } = database;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

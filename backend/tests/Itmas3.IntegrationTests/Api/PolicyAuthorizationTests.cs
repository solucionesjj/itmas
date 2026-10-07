using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Authorization.Permissions.SynchronizeCatalog;
using Itmas3.Domain.Authorization;
using Itmas3.Infrastructure.Persistence;
using Itmas3.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Itmas3.IntegrationTests.Api;

/// <summary>SPEC-0001 acceptance criteria verified through the HTTP API (no UI involved: CA-15).</summary>
public sealed class PolicyAuthorizationTests(PostgreSqlFixture database) : ApiTestBase(database)
{
    private static readonly Uri Users = new("/api/v1/users", UriKind.Relative);
    private static readonly Uri Policies = new("/api/v1/policies", UriKind.Relative);

    [Fact]
    public async Task CA01_new_users_have_no_privileges()
    {
        var client = await Factory.CreateAuthenticatedClientAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Users, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Policies, Ct)).StatusCode);
    }

    [Fact]
    public async Task CA13_root_is_authorized_for_everything_without_policies()
    {
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);

        Assert.Equal(HttpStatusCode.OK, (await root.GetAsync(Users, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await root.GetAsync(new Uri("/api/test/permission", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await root.GetAsync(new Uri("/api/v1/audit-events", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task CA04_to_CA09_and_CA16_policies_grant_union_of_permissions_and_changes_apply_on_next_request()
    {
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        var target = await Factory.CreateUserAsync();
        var client = Factory.CreateClientFor(target);

        var readers = await CreatePolicyAsync(root, "users.read", "policies.read");
        var auditors = await CreatePolicyAsync(root, "users.read", "audit.read");

        await Assign(root, target.Id, readers.Id);
        await Assign(root, target.Id, auditors.Id);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Users, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/api/v1/audit-events", UriKind.Relative), Ct)).StatusCode);

        // CA-16: traceability permission → policies.
        var effective = await ReadJsonAsync(await root.GetAsync(new Uri($"/api/v1/users/{target.Id}/effective-permissions", UriKind.Relative), Ct));
        var grants = effective.GetProperty("permissions").EnumerateArray()
            .ToDictionary(item => item.GetProperty("permission").GetString()!, item => item.GetProperty("grantedBy").GetArrayLength());
        Assert.Equal(2, grants["users.read"]);
        Assert.Equal(1, grants["audit.read"]);
        Assert.Equal(1, grants["policies.read"]);

        // CA-8: removing a policy keeps permissions still granted by another one.
        await Revoke(root, target.Id, auditors.Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Users, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(new Uri("/api/v1/audit-events", UriKind.Relative), Ct)).StatusCode);

        // CA-9: changing a policy affects its users on the next request.
        await UpdatePolicyAsync(root, readers.Id, readers.ETag, isActive: true, "policies.read");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Users, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Policies, Ct)).StatusCode);
    }

    [Fact]
    public async Task RF12_inactive_policies_grant_nothing()
    {
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        var target = await Factory.CreateUserAsync();
        var policy = await CreatePolicyAsync(root, "users.read");
        await Assign(root, target.Id, policy.Id);

        await UpdatePolicyAsync(root, policy.Id, policy.ETag, isActive: false, "users.read");

        Assert.Equal(HttpStatusCode.Forbidden, (await Factory.CreateClientFor(target).GetAsync(Users, Ct)).StatusCode);
    }

    [Fact]
    public async Task Session_exposes_effective_permissions_for_the_ui()
    {
        var user = await Factory.CreateUserAsync(permissions: ["users.read", "audit.read"]);

        var session = await ReadJsonAsync(await Factory.CreateClientFor(user).GetAsync(new Uri("/auth/session", UriKind.Relative), Ct));

        Assert.Equal(["audit.read", "users.read"], session.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()));
    }

    [Theory]
    [InlineData("does.not.exist")]
    [InlineData("authorization.root.manage")]
    public async Task CA03_policies_only_accept_existing_grantable_permissions(string permission)
    {
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);

        var response = await root.PostAsJsonAsync(Policies, new { name = Unique("Inválida"), permissions = new[] { permission } }, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CA18_deactivated_users_lose_their_session_on_the_next_request()
    {
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        var user = await Factory.CreateUserAsync(permissions: "users.read");
        var client = Factory.CreateClientFor(user);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Users, Ct)).StatusCode);

        var response = await root.PatchAsJsonAsync(new Uri($"/api/v1/users/{user.Id}/status", UriKind.Relative), new { isActive = false }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Users, Ct)).StatusCode);
        var session = await ReadJsonAsync(await client.GetAsync(new Uri("/auth/session", UriKind.Relative), Ct));
        Assert.False(session.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task CA21_delegated_administrators_cannot_escalate_privileges()
    {
        var admin = await Factory.CreateUserAsync(permissions:
            ["users.read", "users.update", "users.policies.assign", "policies.read", "policies.create", "policies.update"]);
        var adminClient = Factory.CreateClientFor(admin);
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        var other = await Factory.CreateUserAsync();
        var rootUser = await Factory.CreateUserAsync(isRoot: true);
        var harmless = await CreatePolicyAsync(root, "users.read");
        var powerful = await CreatePolicyAsync(root, "audit.read");

        // RN-9: not on themselves.
        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.PutAsync(PolicyAssignment(admin.Id, harmless.Id), null, Ct)).StatusCode);
        // RN-11: not a policy with permissions they lack.
        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.PutAsync(PolicyAssignment(other.Id, powerful.Id), null, Ct)).StatusCode);
        // RN-10: not creating policies with permissions they lack.
        var create = await adminClient.PostAsJsonAsync(Policies, new { name = Unique("Escalada"), permissions = new[] { "audit.read" } }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        // RN-12: not modifying a Root.
        var deactivateRoot = await adminClient.PatchAsJsonAsync(new Uri($"/api/v1/users/{rootUser.Id}/status", UriKind.Relative), new { isActive = false }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, deactivateRoot.StatusCode);
        // Root management is not grantable.
        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.PutAsync(new Uri($"/api/v1/users/{other.Id}/root", UriKind.Relative), null, Ct)).StatusCode);

        // Allowed: assigning a policy whose permissions they hold, to someone else.
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PutAsync(PolicyAssignment(other.Id, harmless.Id), null, Ct)).StatusCode);

        // Denied attempts are audited.
        var audit = await ReadJsonAsync(await root.GetAsync(new Uri($"/api/v1/audit-events?actorUserId={admin.Id}&pageSize=100", UriKind.Relative), Ct));
        var results = audit.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("result").GetString()).ToList();
        Assert.Equal(4, results.Count(result => result == "denied"));
        Assert.Contains("success", results);
    }

    [Fact]
    public async Task CA14_administrative_changes_are_audited_with_before_and_after()
    {
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        var policy = await CreatePolicyAsync(root, "users.read");
        await UpdatePolicyAsync(root, policy.Id, policy.ETag, isActive: true, "users.read", "policies.read");

        var audit = await ReadJsonAsync(await root.GetAsync(new Uri($"/api/v1/audit-events?resourceType=policy&resourceId={policy.Id}", UriKind.Relative), Ct));
        var events = audit.GetProperty("items").EnumerateArray().ToList();

        var updated = Assert.Single(events, item => item.GetProperty("action").GetString() == "policy.updated");
        Assert.Equal("success", updated.GetProperty("result").GetString());
        Assert.Contains("users.read", updated.GetProperty("before").GetString(), StringComparison.Ordinal);
        Assert.Contains("policies.read", updated.GetProperty("after").GetString(), StringComparison.Ordinal);
        Assert.Single(events, item => item.GetProperty("action").GetString() == "policy.created");
        Assert.False(string.IsNullOrEmpty(updated.GetProperty("actorDisplayName").GetString()));
    }

    [Fact]
    public async Task CA23_the_runtime_role_cannot_alter_the_audit_trail()
    {
        await using var connection = new NpgsqlConnection(Database.ConnectionStringFor(PostgreSqlFixture.AppUser, Database.AppPassword));
        await connection.OpenAsync(Ct);

        foreach (var sql in new[] { "UPDATE app.audit_events SET action = 'x'", "DELETE FROM app.audit_events", "TRUNCATE app.audit_events" })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
        }
    }

    [Fact]
    public async Task CA24_concurrent_policy_edits_are_detected()
    {
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        var policy = await CreatePolicyAsync(root, "users.read");

        await UpdatePolicyAsync(root, policy.Id, policy.ETag, isActive: true, "users.read", "policies.read");
        var stale = await SendUpdateAsync(root, policy.Id, policy.ETag, isActive: true, "users.read");
        var missing = await SendUpdateAsync(root, policy.Id, etag: null, isActive: true, "users.read");

        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
    }

    [Fact]
    public async Task RF14_assigned_policies_cannot_be_deleted()
    {
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        var user = await Factory.CreateUserAsync();
        var policy = await CreatePolicyAsync(root, "users.read");
        await Assign(root, user.Id, policy.Id);

        Assert.Equal(HttpStatusCode.Conflict, (await root.DeleteAsync(new Uri($"/api/v1/policies/{policy.Id}", UriKind.Relative), Ct)).StatusCode);

        await Revoke(root, user.Id, policy.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await root.DeleteAsync(new Uri($"/api/v1/policies/{policy.Id}", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task RN07_policy_names_are_unique_ignoring_case()
    {
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        var name = Unique("Duplicada");
        await root.PostAsJsonAsync(Policies, new { name, permissions = new[] { "users.read" } }, Ct);

        var duplicate = await root.PostAsJsonAsync(Policies, new { name = name.ToUpperInvariant(), permissions = new[] { "users.read" } }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task RF18_root_can_promote_and_revoke_root_while_another_root_remains()
    {
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        var user = await Factory.CreateUserAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await root.PutAsync(new Uri($"/api/v1/users/{user.Id}/root", UriKind.Relative), null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Factory.CreateClientFor(user).GetAsync(Users, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await root.DeleteAsync(new Uri($"/api/v1/users/{user.Id}/root", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Factory.CreateClientFor(user).GetAsync(Users, Ct)).StatusCode);
    }

    [Fact]
    public async Task Users_can_be_searched_and_filtered()
    {
        var root = await Factory.CreateAuthenticatedClientAsync(isRoot: true);
        var user = await Factory.CreateUserAsync();

        var page = await ReadJsonAsync(await root.GetAsync(
            new Uri($"/api/v1/users?search={Uri.EscapeDataString(user.DisplayName)}&status=without-policies", UriKind.Relative), Ct));

        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(user.Id, item.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await root.GetAsync(new Uri("/api/v1/users?status=bogus", UriKind.Relative), Ct)).StatusCode);
    }

    private sealed record CreatedPolicy(Guid Id, string ETag);

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static Uri PolicyAssignment(Guid userId, Guid policyId) => new($"/api/v1/users/{userId}/policies/{policyId}", UriKind.Relative);

    private static async Task<CreatedPolicy> CreatePolicyAsync(HttpClient client, params string[] permissions)
    {
        var response = await client.PostAsJsonAsync(Policies, new { name = Unique("Política"), description = "Prueba", permissions }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return new CreatedPolicy(body.GetProperty("id").GetGuid(), response.Headers.ETag!.Tag);
    }

    private static async Task UpdatePolicyAsync(HttpClient client, Guid id, string etag, bool isActive, params string[] permissions)
    {
        var response = await SendUpdateAsync(client, id, etag, isActive, permissions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendUpdateAsync(HttpClient client, Guid id, string? etag, bool isActive, params string[] permissions)
    {
        var current = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/v1/policies/{id}", UriKind.Relative), Ct);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/policies/{id}")
        {
            Content = JsonContent.Create(new { name = current.GetProperty("name").GetString(), description = "Prueba", isActive, permissions }),
        };
        if (etag is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));
        }

        return await client.SendAsync(request, Ct);
    }

    private static async Task Assign(HttpClient client, Guid userId, Guid policyId) =>
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync(PolicyAssignment(userId, policyId), null, Ct)).StatusCode);

    private static async Task Revoke(HttpClient client, Guid userId, Guid policyId) =>
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(PolicyAssignment(userId, policyId), Ct)).StatusCode);
}

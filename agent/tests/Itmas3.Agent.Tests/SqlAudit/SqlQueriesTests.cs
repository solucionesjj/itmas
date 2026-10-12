using System.Reflection;
using Itmas3.Agent.Collectors.SqlAudit;

namespace Itmas3.Agent.Tests.SqlAudit;

/// <summary>
/// The catalog queries are fixed text that never reads authentication data (SPEC-0005 RF-6, CA-5, constitution §13.6).
/// The integration tests prove they work with the minimum privileges (in MySQL even without access to those columns).
/// </summary>
public sealed class SqlQueriesTests
{
    public static TheoryData<string, string> Queries()
    {
        var data = new TheoryData<string, string>();
        foreach (var type in typeof(SqlAuditCollector).Assembly.GetTypes().Where(type => type.Name.EndsWith("Queries", StringComparison.Ordinal)))
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Where(field => field.FieldType == typeof(string)))
            {
                data.Add($"{type.Name}.{field.Name}", (string)field.GetValue(null)!);
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Where(property => property.PropertyType == typeof(IReadOnlyList<string>)))
            {
                foreach (var (query, index) in ((IReadOnlyList<string>)property.GetValue(null)!).Select((query, index) => (query, index)))
                {
                    data.Add($"{type.Name}.{property.Name}[{index}]", query);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public void Queries_never_select_passwords_hashes_or_user_data(string name, string query)
    {
        Assert.False(string.IsNullOrWhiteSpace(query), name);
        foreach (var forbidden in new[] { "password_hash", "PasswordHash", "authentication_string", "rolpassword", "pg_authid", "pg_shadow", "User_attributes", "Password,", " USE " })
        {
            Assert.DoesNotContain(forbidden, query, StringComparison.OrdinalIgnoreCase);
        }

        // MariaDB: only selected keys of the Priv JSON, never the whole value.
        Assert.DoesNotMatch(@"(?<!\()\bPriv\b\s*(,|FROM)", query);
        Assert.DoesNotMatch(@"(?i)\b(insert|update|delete|merge|drop|alter|create|grant|revoke|exec|execute)\s", query.Replace("SET SESSION TRANSACTION READ ONLY", string.Empty, StringComparison.Ordinal)
            .Replace("privilege_type", string.Empty, StringComparison.OrdinalIgnoreCase));
    }
}

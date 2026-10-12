using System.Text.Json;
using System.Text.Json.Serialization;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Configuration;

namespace Itmas3.Agent.SqlAudit;

/// <summary>
/// One configured engine connection (SPEC-0005 RF-2), as stored on the device. Nothing here is secret: the password lives
/// in its own protected file and is never part of this record, of the logs, of the command line or of the API calls.
/// </summary>
public sealed record SqlConnectionDefinition(
    Guid Id,
    string Alias,
    string Engine,
    string Host,
    int? Port,
    string? InstanceName,
    string? Database,
    string User,
    string Tls,
    string? PinnedSha256,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Outcome of the last collections of a connection (shown by <c>sql list</c>, drives the retries of RF-8).</summary>
/// <param name="LastOutcome">complete, partial, unreachable (instance unavailable, snapshot sent) or failed (not sent).</param>
public sealed record SqlConnectionResult(
    DateTimeOffset? LastAttemptAt = null,
    DateTimeOffset? LastSuccessAt = null,
    string? LastOutcome = null,
    int UnavailableSections = 0,
    int ConsecutiveFailures = 0,
    DateTimeOffset? NextRetryAt = null,
    string? LastError = null);

public static class SqlOutcomes
{
    public const string Complete = "complete";
    public const string Partial = "partial";
    public const string Unreachable = "unreachable";
    public const string Failed = "failed";
}

public interface ISqlConnectionStore
{
    IReadOnlyList<SqlConnectionDefinition> List();

    SqlConnectionDefinition? Find(string alias);

    string? LoadPassword(SqlConnectionDefinition connection);

    /// <summary>Where the DER copy of a pinned SQL Server certificate lives (read by SqlClient).</summary>
    string CertificatePath(Guid id);

    /// <summary>Temporary location used to test a new pinned certificate before it is saved.</summary>
    string PendingCertificatePath(Guid id);

    /// <summary>
    /// Adds or replaces a connection. <paramref name="password"/> null keeps the stored one; a pending certificate
    /// (<see cref="PendingCertificatePath"/>) becomes the pinned one.
    /// </summary>
    void Save(SqlConnectionDefinition connection, string? password);

    /// <summary>Removes the connection, its credential, its pinned certificate and its results.</summary>
    bool Remove(string alias);

    /// <summary>Writes a certificate to pin at <see cref="PendingCertificatePath"/> so it can be tested before saving.</summary>
    void StagePinnedCertificate(Guid id, byte[] der);

    /// <summary>Deletes a pending certificate left by a failed <c>sql add</c>/<c>sql update</c>.</summary>
    void DiscardPendingCertificate(Guid id);

    /// <summary>Settings for the collectors; with <paramref name="pendingCertificate"/> the staged certificate is pinned.</summary>
    SqlConnectionSettings Settings(SqlConnectionDefinition connection, bool pendingCertificate = false);

    IReadOnlyDictionary<Guid, SqlConnectionResult> LoadResults();

    void SaveResult(Guid id, SqlConnectionResult result);
}

/// <summary>
/// Connections under &lt;data&gt;/sql (ADR-0014 §4): <c>connections.json</c> (written only by the CLI), <c>results.json</c>
/// (written only by the service), <c>credentials/&lt;id&gt;</c> with each password protected exactly like the device API key
/// (DPAPI machine scope on Windows; 0600 in a 0700 root-owned directory on Linux and macOS) and
/// <c>certificates/&lt;id&gt;.cer</c> for pinned SQL Server certificates. Every file is replaced atomically.
/// </summary>
public sealed class SqlConnectionStore(AgentPaths paths) : ISqlConnectionStore
{
    private string Root => Path.Combine(paths.DataDirectory, "sql");

    private string ConnectionsPath => Path.Combine(Root, "connections.json");

    private string ResultsPath => Path.Combine(Root, "results.json");

    private string CredentialsDirectory => Path.Combine(Root, "credentials");

    private string CertificatesDirectory => Path.Combine(Root, "certificates");

    public IReadOnlyList<SqlConnectionDefinition> List() =>
        File.Exists(ConnectionsPath)
            ? JsonSerializer.Deserialize(File.ReadAllText(ConnectionsPath), SqlStoreJsonContext.Default.ListSqlConnectionDefinition) ?? []
            : [];

    public SqlConnectionDefinition? Find(string alias) =>
        List().FirstOrDefault(connection => string.Equals(connection.Alias, alias, StringComparison.OrdinalIgnoreCase));

    public string? LoadPassword(SqlConnectionDefinition connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return SecureFiles.ReadSecret(CredentialPath(connection.Id), trim: false);
    }

    public string CertificatePath(Guid id) => Path.Combine(CertificatesDirectory, $"{id:N}.cer");

    public string PendingCertificatePath(Guid id) => Path.Combine(CertificatesDirectory, $"{id:N}.pending.cer");

    public void Save(SqlConnectionDefinition connection, string? password)
    {
        ArgumentNullException.ThrowIfNull(connection);
        EnsureDirectories();

        // Credential and certificate first: a connection listed in connections.json always has them.
        if (password is not null)
        {
            SecureFiles.WriteSecret(CredentialPath(connection.Id), password);
        }

        var pending = PendingCertificatePath(connection.Id);
        if (File.Exists(pending))
        {
            File.Move(pending, CertificatePath(connection.Id), overwrite: true);
        }
        else if (connection.Tls != Collectors.SqlAudit.Model.SqlTlsModes.Pinned)
        {
            TryDelete(CertificatePath(connection.Id));
        }

        var connections = List().Where(existing => existing.Id != connection.Id).Append(connection).OrderBy(existing => existing.Alias, StringComparer.OrdinalIgnoreCase).ToList();
        SecureFiles.WriteText(ConnectionsPath, JsonSerializer.Serialize(connections, SqlStoreJsonContext.Default.ListSqlConnectionDefinition));
    }

    public bool Remove(string alias)
    {
        if (Find(alias) is not { } connection)
        {
            return false;
        }

        var remaining = List().Where(existing => existing.Id != connection.Id).ToList();
        SecureFiles.WriteText(ConnectionsPath, JsonSerializer.Serialize(remaining, SqlStoreJsonContext.Default.ListSqlConnectionDefinition));
        TryDelete(CredentialPath(connection.Id));
        TryDelete(CertificatePath(connection.Id));
        TryDelete(PendingCertificatePath(connection.Id));
        return true;
    }

    public void StagePinnedCertificate(Guid id, byte[] der)
    {
        EnsureDirectories();
        SecureFiles.WriteData(PendingCertificatePath(id), der);
    }

    public void DiscardPendingCertificate(Guid id) => TryDelete(PendingCertificatePath(id));

    public IReadOnlyDictionary<Guid, SqlConnectionResult> LoadResults()
    {
        try
        {
            return File.Exists(ResultsPath)
                ? JsonSerializer.Deserialize(File.ReadAllText(ResultsPath), SqlStoreJsonContext.Default.DictionaryGuidSqlConnectionResult) ?? []
                : new Dictionary<Guid, SqlConnectionResult>();
        }
        catch (JsonException)
        {
            // A corrupt result file only means "never collected": the next cycle collects every connection.
            return new Dictionary<Guid, SqlConnectionResult>();
        }
    }

    public void SaveResult(Guid id, SqlConnectionResult result)
    {
        EnsureDirectories();
        var configured = List().Select(connection => connection.Id).ToHashSet();
        var results = LoadResults()
            .Where(pair => pair.Key != id && configured.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        results[id] = result;
        SecureFiles.WriteText(ResultsPath, JsonSerializer.Serialize(results, SqlStoreJsonContext.Default.DictionaryGuidSqlConnectionResult));
    }

    public SqlConnectionSettings Settings(SqlConnectionDefinition connection, bool pendingCertificate = false)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return ToSettings(connection, pendingCertificate ? PendingCertificatePath(connection.Id) : CertificatePath(connection.Id));
    }

    public static SqlConnectionSettings ToSettings(SqlConnectionDefinition connection, string certificatePath)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new SqlConnectionSettings(
            connection.Alias,
            connection.Engine,
            connection.Host,
            connection.Port,
            connection.InstanceName,
            connection.Database,
            connection.User,
            connection.Tls,
            connection.PinnedSha256,
            connection.Tls == Collectors.SqlAudit.Model.SqlTlsModes.Pinned ? certificatePath : null);
    }

    private string CredentialPath(Guid id) => Path.Combine(CredentialsDirectory, id.ToString("N"));

    private void EnsureDirectories()
    {
        SecureFiles.EnsureDirectory(paths.DataDirectory);
        SecureFiles.EnsureDirectory(Root);
        SecureFiles.EnsureDirectory(CredentialsDirectory);
        SecureFiles.EnsureDirectory(CertificatesDirectory);
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(List<SqlConnectionDefinition>))]
[JsonSerializable(typeof(Dictionary<Guid, SqlConnectionResult>))]
internal sealed partial class SqlStoreJsonContext : JsonSerializerContext;

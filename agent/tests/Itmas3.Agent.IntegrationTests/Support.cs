using System.Data.Common;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DotNet.Testcontainers.Containers;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.Model;

// Engines run one at a time: several SQL Server containers at once do not fit in a developer machine.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

namespace Itmas3.Agent.IntegrationTests;

/// <summary>Shared helpers: random secrets (constitution §4.6), throw-away TLS certificates and readiness polling.</summary>
internal static class Support
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Random password generated per run; satisfies the SQL Server complexity policy. Never stored anywhere.</summary>
    public static string RandomPassword() => "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    /// <summary>Without Docker (e.g. the agent Dockerfile test stage) these tests are skipped with the reason.</summary>
    public static string? DockerUnavailableReason() =>
        DotNet.Testcontainers.Configurations.TestcontainersSettings.OS.DockerEndpointAuthConfig is null
            ? "Docker no está disponible: las pruebas con motores en contenedor requieren Docker."
            : null;

    public static async Task WaitUntilAsync(Func<CancellationToken, Task> probe, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            try
            {
                await probe(cancellationToken);
                return;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (DateTimeOffset.UtcNow > until)
                {
                    throw new TimeoutException("The engine did not become ready.", exception);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    public static async Task ExecuteAsync(DbConnection connection, params string[] statements)
    {
        foreach (var statement in statements)
        {
            await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Test setup statements written by the test itself (random hexadecimal passwords only).
            command.CommandText = statement;
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync(Ct);
        }
    }

    /// <summary>Skips the engine tests when the container cannot run here (e.g. amd64-only images without emulation).</summary>
    public static async Task<string?> UnrunnableReasonAsync(IContainer container)
    {
        if (container.State != TestcontainersStates.Exited)
        {
            return null;
        }

        var (stdout, stderr) = await container.GetLogsAsync(ct: Ct);
        return (stdout + stderr).Contains("exec format error", StringComparison.OrdinalIgnoreCase)
            ? $"La imagen {container.Image.FullName} es solo amd64 y este Docker no tiene emulación amd64 (exec format error). "
              + "En Apple Silicon active Docker Desktop → Settings → General → 'Use Rosetta for x86_64/amd64 emulation' y repita."
            : $"El contenedor {container.Image.FullName} terminó al arrancar: {(stdout + stderr).Trim()[..Math.Min(400, (stdout + stderr).Trim().Length)]}";
    }

    public static SqlConnectionSettings Settings(
        string engine, string host, int port, string user, string tls, string? pinnedSha256 = null, string? certificatePath = null, string? database = null) =>
        new("prueba", engine, host, port, null, database, user, tls, pinnedSha256, certificatePath);
}

/// <summary>Self-signed RSA server certificate for localhost, created for one test run (nothing is kept in the repository).</summary>
public sealed class TestCertificate : IDisposable
{
    private TestCertificate(X509Certificate2 certificate, string keyPem)
    {
        Certificate = certificate;
        KeyPem = keyPem;
    }

    public X509Certificate2 Certificate { get; }

    public string CertificatePem => Certificate.ExportCertificatePem();

    public string KeyPem { get; }

    public string Thumbprint => SqlTls.Thumbprint(Certificate);

    public static TestCertificate Create(string commonName = "localhost")
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(commonName);
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
        return new TestCertificate(X509CertificateLoader.LoadCertificate(certificate.RawData), key.ExportPkcs8PrivateKeyPem());
    }

    /// <summary>DER copy for SqlClient's ServerCertificate (pinned mode), in a temporary file.</summary>
    public string WriteDer(string directory)
    {
        var path = Path.Combine(directory, $"{Thumbprint[..12]}.cer");
        File.WriteAllBytes(path, Certificate.RawData);
        return path;
    }

    public void Dispose() => Certificate.Dispose();
}

/// <summary>Assertions shared by every engine.</summary>
internal static class SnapshotAssert
{
    public static SqlAccountInfo Account(SqlAuditSnapshot snapshot, string name, string? database = null) =>
        Assert.Single(snapshot.Accounts, account => account.Name == name && account.Database == database);

    public static List<SqlPermissionInfo> Of(SqlAuditSnapshot snapshot, SqlAccountInfo account) =>
        snapshot.Permissions.Where(permission => permission.GranteeRef == account.Ref).ToList();

    public static List<string> Roles(SqlAuditSnapshot snapshot, SqlAccountInfo account) =>
        snapshot.RoleMemberships.Where(membership => membership.MemberRef == account.Ref)
            .Select(membership => snapshot.Accounts.Single(role => role.Ref == membership.RoleRef).Name)
            .ToList();

    /// <summary>The snapshot passes the contract rules the API enforces (refs exist, sections valid, no duplicates).</summary>
    public static void IsValidContract(SqlAuditSnapshot snapshot)
    {
        var refs = snapshot.Accounts.Select(account => account.Ref).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(snapshot.Accounts.Count, refs.Count);
        Assert.All(snapshot.RoleMemberships, membership => Assert.True(refs.Contains(membership.MemberRef) && refs.Contains(membership.RoleRef)));
        Assert.All(snapshot.Permissions, permission => Assert.True(refs.Contains(permission.GranteeRef) && (permission.GrantorRef is null || refs.Contains(permission.GrantorRef))));
        Assert.All(snapshot.Permissions, permission => Assert.Matches("^[A-Z][A-Z0-9_ ]{0,127}$", permission.Permission));
        var databases = snapshot.Databases.Select(database => database.Name).ToHashSet(StringComparer.Ordinal);
        Assert.All(snapshot.Accounts.Where(account => account.Level == SqlAccountLevels.Database), account => Assert.Contains(account.Database!, databases));
        Assert.All(snapshot.Unavailable.Where(item => item.Section.StartsWith("database:", StringComparison.Ordinal)), item => Assert.Contains(item.Section["database:".Length..], databases));
        var identities = snapshot.Permissions.Select(permission => (permission.GranteeRef, permission.Securable, permission.Permission, permission.State)).ToList();
        Assert.Equal(identities.Count, identities.Distinct().Count());
        Assert.NotNull(SqlAuditJson.Deserialize(SqlAuditJson.Serialize(snapshot)));
    }
}

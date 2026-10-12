using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.Collectors.SqlAudit.MySql;
using Itmas3.Agent.Collectors.SqlAudit.PostgreSql;
using Itmas3.Agent.Collectors.SqlAudit.SqlServer;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace Itmas3.Agent.Tests.SqlAudit;

/// <summary>
/// Channel protection (SPEC-0005 RF-3, ADR-0015, CA-4): always encrypted, verify/pinned/unverified, connection strings
/// only from the drivers' builders, and failure classification. End-to-end TLS is in the integration tests.
/// </summary>
public sealed class SqlTlsTests
{
    private const string Password = "placeholder-not-a-secret";

    [Fact]
    public void Verify_accepts_only_certificates_without_policy_errors()
    {
        using var certificate = SelfSigned();
        var callback = SqlTls.ValidationCallback(SqlTlsModes.Verify, null);

        Assert.True(callback(this, certificate, null, SslPolicyErrors.None));
        Assert.False(callback(this, certificate, null, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(callback(this, certificate, null, SslPolicyErrors.RemoteCertificateNameMismatch));
        Assert.False(callback(this, null, null, SslPolicyErrors.None));
    }

    [Fact]
    public void Pinned_accepts_only_the_pinned_certificate_whatever_the_chain_says()
    {
        using var pinned = SelfSigned();
        using var other = SelfSigned();
        var callback = SqlTls.ValidationCallback(SqlTlsModes.Pinned, SqlTls.Thumbprint(pinned));

        Assert.True(callback(this, pinned, null, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(callback(this, other, null, SslPolicyErrors.None)); // even a trusted one
        Assert.False(callback(this, null, null, SslPolicyErrors.None));
    }

    [Fact]
    public void Unverified_accepts_any_presented_certificate_but_still_requires_tls()
    {
        using var certificate = SelfSigned();
        var callback = SqlTls.ValidationCallback(SqlTlsModes.Unverified, null);

        Assert.True(callback(this, certificate, null, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch));
        Assert.False(callback(this, null, null, SslPolicyErrors.RemoteCertificateNotAvailable));
    }

    [Theory]
    [InlineData("AB:CD:EF:01:23:45:67:89:AB:CD:EF:01:23:45:67:89:AB:CD:EF:01:23:45:67:89:AB:CD:EF:01:23:45:67:89", "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789", "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("ABCDEF", null)]
    [InlineData("ZZCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789", null)]
    [InlineData(null, null)]
    public void Thumbprints_are_normalized(string? value, string? expected) => Assert.Equal(expected, SqlTls.NormalizeThumbprint(value));

    [Fact]
    public void Certificates_load_from_pem_and_der_files_with_the_same_fingerprint()
    {
        using var certificate = SelfSigned();
        var directory = Directory.CreateTempSubdirectory("itmas-sql-tls").FullName;
        try
        {
            var der = Path.Combine(directory, "server.cer");
            var pem = Path.Combine(directory, "server.pem");
            File.WriteAllBytes(der, certificate.RawData);
            File.WriteAllText(pem, certificate.ExportCertificatePem());

            using var fromDer = SqlTls.LoadCertificate(der);
            using var fromPem = SqlTls.LoadCertificate(pem);
            Assert.Equal(SqlTls.Thumbprint(certificate), SqlTls.Thumbprint(fromDer));
            Assert.Equal(SqlTls.Thumbprint(certificate), SqlTls.Thumbprint(fromPem));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(SqlTlsModes.Verify, false, "")]
    [InlineData(SqlTlsModes.Pinned, false, "/var/lib/itmas-agent/sql/certificates/x.cer")]
    [InlineData(SqlTlsModes.Unverified, true, "")]
    public void SqlServer_is_always_encrypted_mandatory(string mode, bool trustServerCertificate, string serverCertificate)
    {
        var builder = SqlServerEngine.Builder(Settings(SqlEngines.SqlServer, mode) with { PinnedCertificatePath = mode == SqlTlsModes.Pinned ? serverCertificate : null }, Password, "master");

        Assert.Equal(SqlConnectionEncryptOption.Mandatory, builder.Encrypt);
        Assert.Equal(trustServerCertificate, builder.TrustServerCertificate);
        Assert.Equal(serverCertificate, builder.ServerCertificate);
        Assert.Equal((ApplicationIntent.ReadOnly, "ITMas Agent", false, false), (builder.ApplicationIntent, builder.ApplicationName, builder.IntegratedSecurity, builder.PersistSecurityInfo));
        Assert.Equal(30, builder.CommandTimeout);
        Assert.Equal("tcp:db.example.test,1433", builder.DataSource);
    }

    [Fact]
    public void SqlServer_named_instances_and_hostile_database_names_stay_inside_their_fields()
    {
        var settings = Settings(SqlEngines.SqlServer, SqlTlsModes.Verify) with { Port = null, InstanceName = "SQLEXPRESS" };
        var builder = SqlServerEngine.Builder(settings, Password, "Ventas']; Encrypt=False;--");

        Assert.Equal(@"tcp:db.example.test\SQLEXPRESS", builder.DataSource);
        var parsed = new SqlConnectionStringBuilder(builder.ConnectionString);
        Assert.Equal("Ventas']; Encrypt=False;--", parsed.InitialCatalog);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, parsed.Encrypt);
        Assert.Throws<SqlAuditException>(() => SqlServerEngine.Builder(Settings(SqlEngines.SqlServer, SqlTlsModes.Pinned), Password, "master"));
    }

    [Theory]
    [InlineData(SqlTlsModes.Verify)]
    [InlineData(SqlTlsModes.Pinned)]
    [InlineData(SqlTlsModes.Unverified)]
    public void PostgreSql_requires_ssl_and_disables_gss_encryption_in_every_mode(string mode)
    {
        var builder = PostgreSqlEngine.Builder(Settings(SqlEngines.PostgreSql, mode), Password, "x;SslMode=Disable");

        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.Equal(GssEncryptionMode.Disable, builder.GssEncryptionMode);
        Assert.Equal("-c default_transaction_read_only=on", builder.Options);
        var parsed = new NpgsqlConnectionStringBuilder(builder.ConnectionString);
        Assert.Equal("x;SslMode=Disable", parsed.Database);
        Assert.Equal(SslMode.Require, parsed.SslMode);
    }

    [Theory]
    [InlineData(SqlTlsModes.Verify)]
    [InlineData(SqlTlsModes.Pinned)]
    [InlineData(SqlTlsModes.Unverified)]
    public void MySql_requires_ssl_and_never_retrieves_the_rsa_key_in_any_mode(string mode)
    {
        var builder = MySqlEngine.Builder(Settings(SqlEngines.MySql, mode) with { User = "agent;SslMode=None" }, Password);

        Assert.Equal(MySqlSslMode.Required, builder.SslMode);
        Assert.False(builder.AllowPublicKeyRetrieval);
        Assert.False(builder.AllowLoadLocalInfile);
        Assert.Equal(MySqlServerRedirectionMode.Disabled, builder.ServerRedirectionMode);
        var parsed = new MySqlConnectionStringBuilder(builder.ConnectionString);
        Assert.Equal("agent;SslMode=None", parsed.UserID);
        Assert.Equal(MySqlSslMode.Required, parsed.SslMode);
    }

    [Theory]
    [InlineData("sql01.example.internal", true)]
    [InlineData("10.0.0.15", true)]
    [InlineData("::1", true)]
    [InlineData("host;Encrypt=False", false)]
    [InlineData("host,1433", false)]
    [InlineData(@"host\instance", false)]
    [InlineData("host name", false)]
    [InlineData("", false)]
    public void Hosts_cannot_carry_connection_string_syntax(string host, bool valid) => Assert.Equal(valid, SqlConnectionRules.IsValidHost(host));

    [Fact]
    public void Settings_validation_rejects_inconsistent_definitions()
    {
        Assert.Null(SqlConnectionRules.Validate(Settings(SqlEngines.SqlServer, SqlTlsModes.Verify)));
        Assert.NotNull(SqlConnectionRules.Validate(Settings(SqlEngines.MySql, SqlTlsModes.Verify) with { InstanceName = "X" }));
        Assert.NotNull(SqlConnectionRules.Validate(Settings(SqlEngines.MySql, SqlTlsModes.Verify) with { Database = "db" }));
        Assert.NotNull(SqlConnectionRules.Validate(Settings(SqlEngines.PostgreSql, SqlTlsModes.Pinned) with { PinnedSha256 = null }));
        Assert.NotNull(SqlConnectionRules.Validate(Settings(SqlEngines.PostgreSql, "plain")));
        Assert.NotNull(SqlConnectionRules.Validate(Settings(SqlEngines.PostgreSql, SqlTlsModes.Verify) with { Alias = "-bad" }));
        Assert.NotNull(SqlConnectionRules.Validate(Settings(SqlEngines.PostgreSql, SqlTlsModes.Verify) with { Port = 70000 }));
    }

    [Fact]
    public void Failures_are_classified_without_engine_messages()
    {
        Assert.Equal(SqlFailureKind.Certificate, PostgreSqlEngine.Classify(new NpgsqlException("x", new AuthenticationException("bad cert")), Fixtures.Ct));
        Assert.Equal(SqlFailureKind.Dns, MySqlEngine.Classify(new InvalidOperationException("x", new SocketException((int)SocketError.HostNotFound)), Fixtures.Ct));
        Assert.Equal(SqlFailureKind.Connection, SqlServerEngine.Classify(new InvalidOperationException("x", new SocketException((int)SocketError.ConnectionRefused)), Fixtures.Ct));
        Assert.Equal(SqlFailureKind.Authentication, PostgreSqlEngine.Classify(new PostgresException("password authentication failed for user \"x\"", "FATAL", "FATAL", PostgresErrorCodes.InvalidPassword), Fixtures.Ct));
        Assert.Equal(SqlFailureKind.Permission, PostgreSqlEngine.Classify(new PostgresException("permission denied for database \"x\"", "FATAL", "FATAL", PostgresErrorCodes.InsufficientPrivilege), Fixtures.Ct));
        Assert.Equal(SqlFailureKind.TlsNotSupported, PostgreSqlEngine.Classify(new NpgsqlException("SSL connection requested. No SSL enabled connection from this host is configured."), Fixtures.Ct));
        Assert.Equal(SqlFailureKind.Timeout, SqlServerEngine.Classify(new TimeoutException(), Fixtures.Ct));
        Assert.Null(SqlServerEngine.Classify(new OperationCanceledException(), new CancellationToken(canceled: true)));
        Assert.Equal(SqlUnavailableReasons.ConnectionFailed, SqlFailures.Reason(SqlFailureKind.TlsNotSupported));
        Assert.Equal(SqlUnavailableReasons.CertificateInvalid, SqlFailures.Reason(SqlFailureKind.Certificate));
    }

    [Fact]
    public void Tds_prelogin_asks_for_encryption_and_detects_servers_without_it()
    {
        var packet = SqlServerCertificateFetcher.PreloginRequest();

        Assert.Equal((byte)0x12, packet[0]);
        Assert.Equal(packet.Length, (packet[2] << 8) | packet[3]);
        Assert.Equal((byte)0x01, SqlServerCertificateFetcher.EncryptionOption(packet.AsSpan(8)));
        byte[] notSupported = [0x00, 0x00, 0x0B, 0x00, 0x06, 0x01, 0x00, 0x11, 0x00, 0x01, 0xFF, 16, 0, 0, 0, 0, 0, 0x02];
        Assert.Equal((byte)0x02, SqlServerCertificateFetcher.EncryptionOption(notSupported));
    }

    [Fact]
    public void Sql_browser_responses_yield_the_tcp_port()
    {
        var response = System.Text.Encoding.ASCII.GetBytes("\u0005\u0000\u0000ServerName;SRV;InstanceName;SQLEXPRESS;IsClustered;No;Version;16.0.1000.6;tcp;49172;;");

        Assert.Equal(49172, SqlServerCertificateFetcher.ParseSsrpPort(response));
        Assert.Null(SqlServerCertificateFetcher.ParseSsrpPort("\u0005\u0000\u0000ServerName;SRV;np;\\\\SRV\\pipe;;"u8));
    }

    internal static X509Certificate2 SelfSigned(string host = "db.example.test")
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={host}", key, HashAlgorithmName.SHA256);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(host);
        request.CertificateExtensions.Add(san.Build());
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static SqlConnectionSettings Settings(string engine, string mode) =>
        new("prueba", engine, "db.example.test", SqlConnectionSettings.DefaultPort(engine), null, null, "itmas_agent", mode,
            mode == SqlTlsModes.Pinned ? new string('A', 64) : null);
}

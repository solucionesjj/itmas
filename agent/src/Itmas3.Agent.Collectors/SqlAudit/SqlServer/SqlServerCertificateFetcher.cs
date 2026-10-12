using System.Buffers.Binary;
using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Itmas3.Agent.Collectors.SqlAudit.SqlServer;

/// <summary>
/// Obtains the TLS certificate a SQL Server presents, without logging in, so a connection can be pinned by its SHA-256
/// (SqlClient only pins by certificate file: <c>ServerCertificate</c>). It sends a TDS PRELOGIN asking for encryption
/// and runs the TLS handshake wrapped in PRELOGIN packets, as TDS 7.x does; the certificate is captured and the
/// connection is closed right after the handshake. No credential is ever sent. Named instances are resolved with SQL
/// Browser (SSRP, UDP 1434). Only used by <c>sql add</c>/<c>sql update</c>; the administrator compares the result with the
/// fingerprint the DBA gave them.
/// </summary>
public static class SqlServerCertificateFetcher
{
    private const byte PreloginPacket = 0x12;
    private const byte EndOfMessage = 0x01;
    private const byte EncryptionNotSupported = 0x02;
    private const int HeaderLength = 8;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public static async Task<X509Certificate2> FetchAsync(string host, int? port, string? instanceName, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        var tcpPort = port ?? await ResolveInstancePortAsync(host, instanceName ?? throw new ArgumentException("A port or an instance is required.", nameof(port)), timeout.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(host, tcpPort, timeout.Token);
        await using var network = client.GetStream();

        await network.WriteAsync(PreloginRequest(), timeout.Token);
        var response = await ReadPacketAsync(network, timeout.Token);
        if (EncryptionOption(response) == EncryptionNotSupported)
        {
            throw new SqlAuditException(SqlFailureKind.TlsNotSupported, "The server does not support encryption.");
        }

        X509Certificate2? captured = null;
        await using var tls = new SslStream(new PreloginTlsStream(network), leaveInnerStreamOpen: true);
        await tls.AuthenticateAsClientAsync(
            new SslClientAuthenticationOptions
            {
                TargetHost = host,
                // Only to capture the certificate: nothing but the handshake goes through this stream.
                RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                {
                    captured = certificate is null ? null : X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
                    return captured is not null;
                },
            },
            timeout.Token);
        return captured ?? throw new SqlAuditException(SqlFailureKind.Certificate, "The server presented no certificate.");
    }

    /// <summary>PRELOGIN with VERSION and ENCRYPTION=ON (MS-TDS 2.2.6.5).</summary>
    internal static byte[] PreloginRequest()
    {
        // Option tokens: VERSION (0x00) at offset 11, length 6; ENCRYPTION (0x01) at offset 17, length 1; terminator.
        byte[] payload = [0x00, 0x00, 0x0B, 0x00, 0x06, 0x01, 0x00, 0x11, 0x00, 0x01, 0xFF, 0, 0, 0, 0, 0, 0, 0x01];
        return Packet(payload);
    }

    internal static byte EncryptionOption(ReadOnlySpan<byte> payload)
    {
        for (var index = 0; index + 5 <= payload.Length && payload[index] != 0xFF; index += 5)
        {
            if (payload[index] == 0x01)
            {
                var offset = BinaryPrimitives.ReadUInt16BigEndian(payload[(index + 1)..]);
                return offset < payload.Length ? payload[offset] : EncryptionNotSupported;
            }
        }

        return EncryptionNotSupported;
    }

    /// <summary>SSRP CLNT_UCAST_INST: asks SQL Browser for the TCP port of a named instance.</summary>
    internal static async Task<int> ResolveInstancePortAsync(string host, string instanceName, CancellationToken cancellationToken)
    {
        using var udp = new UdpClient();
        udp.Connect(host, 1434);
        var request = new byte[instanceName.Length + 2];
        request[0] = 0x04;
        Encoding.ASCII.GetBytes(instanceName, request.AsSpan(1));
        await udp.SendAsync(request, cancellationToken);
        var result = await udp.ReceiveAsync(cancellationToken);
        return ParseSsrpPort(result.Buffer) ?? throw new SqlAuditException(SqlFailureKind.Connection, "SQL Browser did not return a TCP port.");
    }

    internal static int? ParseSsrpPort(ReadOnlySpan<byte> response)
    {
        if (response.Length < 3 || response[0] != 0x05)
        {
            return null;
        }

        var fields = Encoding.ASCII.GetString(response[3..]).Split(';');
        for (var index = 0; index + 1 < fields.Length; index += 2)
        {
            if (string.Equals(fields[index], "tcp", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(fields[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535)
            {
                return port;
            }
        }

        return null;
    }

    private static byte[] Packet(ReadOnlySpan<byte> payload)
    {
        var packet = new byte[HeaderLength + payload.Length];
        packet[0] = PreloginPacket;
        packet[1] = EndOfMessage;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), (ushort)packet.Length);
        packet[6] = 0x01;
        payload.CopyTo(packet.AsSpan(HeaderLength));
        return packet;
    }

    private static async Task<byte[]> ReadPacketAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderLength];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2)) - HeaderLength;
        if (length is < 0 or > 32_768)
        {
            throw new SqlAuditException(SqlFailureKind.Other, "Unexpected TDS packet.");
        }

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        return payload;
    }

    /// <summary>Wraps the TLS handshake in TDS PRELOGIN packets, as SQL Server expects before the login.</summary>
    private sealed class PreloginTlsStream(Stream inner) : Stream
    {
        private int _remaining;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_remaining == 0)
            {
                var header = new byte[HeaderLength];
                await inner.ReadExactlyAsync(header, cancellationToken);
                _remaining = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2)) - HeaderLength;
                if (_remaining <= 0)
                {
                    return 0;
                }
            }

            var read = await inner.ReadAsync(buffer[..Math.Min(buffer.Length, _remaining)], cancellationToken);
            _remaining -= read;
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await inner.WriteAsync(Packet(buffer.Span), cancellationToken);
            await inner.FlushAsync(cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

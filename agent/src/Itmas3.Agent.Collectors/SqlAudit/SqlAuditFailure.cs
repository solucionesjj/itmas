using Itmas3.Agent.Collectors.SqlAudit.Model;

namespace Itmas3.Agent.Collectors.SqlAudit;

/// <summary>Why an engine, or a part of it, could not be read. Drives the CLI explanation and the snapshot reason.</summary>
public enum SqlFailureKind
{
    Dns,
    Connection,
    TlsNotSupported,
    Certificate,
    Authentication,
    Permission,
    Timeout,
    Offline,
    EngineMismatch,
    Other,
}

/// <summary>
/// A failure classified by the engine driver. <see cref="Exception.Message"/> is a fixed technical description: it never
/// contains engine messages, which may carry account, database or object names (SPEC-0005 §14.1: no names in logs).
/// </summary>
public sealed class SqlAuditException : Exception
{
    public SqlAuditException(SqlFailureKind kind, string detail, Exception? inner = null)
        : base(detail, inner)
    {
        Kind = kind;
    }

    public SqlAuditException()
        : this(SqlFailureKind.Other, "SQL audit failure")
    {
    }

    public SqlAuditException(string message)
        : this(SqlFailureKind.Other, message)
    {
    }

    public SqlAuditException(string message, Exception innerException)
        : this(SqlFailureKind.Other, message, innerException)
    {
    }

    public SqlFailureKind Kind { get; }
}

public static class SqlFailures
{
    /// <summary>Reason of the contract (RF-7) for a failure kind.</summary>
    public static string Reason(SqlFailureKind kind) => kind switch
    {
        SqlFailureKind.Dns or SqlFailureKind.Connection or SqlFailureKind.TlsNotSupported => SqlUnavailableReasons.ConnectionFailed,
        SqlFailureKind.Certificate => SqlUnavailableReasons.CertificateInvalid,
        SqlFailureKind.Authentication => SqlUnavailableReasons.AuthenticationFailed,
        SqlFailureKind.Permission => SqlUnavailableReasons.PermissionDenied,
        SqlFailureKind.Timeout => SqlUnavailableReasons.Timeout,
        SqlFailureKind.Offline => SqlUnavailableReasons.Offline,
        _ => SqlUnavailableReasons.Error,
    };

    /// <summary>Explanation for the administrator (Spanish, actionable, without engine messages).</summary>
    public static string Explain(SqlFailureKind kind) => kind switch
    {
        SqlFailureKind.Dns => "no se pudo resolver el nombre del servidor de base de datos. Revise el host.",
        SqlFailureKind.Connection => "no se pudo conectar con el motor (conexión rechazada, puerto cerrado, instancia inexistente o red inaccesible). Revise el host, el puerto o la instancia y el firewall.",
        SqlFailureKind.TlsNotSupported => "el motor no acepta conexiones cifradas (TLS). El agente nunca se conecta sin cifrado: pida al DBA que active TLS en el motor.",
        SqlFailureKind.Certificate => "el certificado TLS del motor no es válido para esta conexión: no es de confianza, no corresponde al host o no es el certificado fijado. "
            + "Use un certificado de confianza o fije el certificado del servidor con --tls pinned (recomendado para certificados autofirmados).",
        SqlFailureKind.Authentication => "el motor rechazó el usuario o la contraseña.",
        SqlFailureKind.Permission => "la cuenta no tiene permiso para conectarse o para leer el catálogo de seguridad. Revise los permisos mínimos de la cuenta del agente en el manual.",
        SqlFailureKind.Timeout => "el motor no respondió a tiempo.",
        SqlFailureKind.Offline => "la base de datos no está en línea.",
        SqlFailureKind.EngineMismatch => "el servidor no es del motor indicado (por ejemplo, un MariaDB configurado como mysql). Corrija el motor.",
        _ => "error inesperado al leer el catálogo del motor.",
    };

    /// <summary>Classification shared by every driver: network, TLS and time-outs surface as framework exceptions.</summary>
    public static SqlFailureKind? ClassifyCommon(Exception exception, CancellationToken cancellationToken)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case SqlAuditException audit:
                    return audit.Kind;
                case System.Security.Authentication.AuthenticationException:
                    return SqlFailureKind.Certificate;
                case System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.HostNotFound or System.Net.Sockets.SocketError.TryAgain or System.Net.Sockets.SocketError.NoData }:
                    return SqlFailureKind.Dns;
                case System.Net.Sockets.SocketException:
                    return SqlFailureKind.Connection;
                case TimeoutException:
                    return SqlFailureKind.Timeout;
                case OperationCanceledException when !cancellationToken.IsCancellationRequested:
                    return SqlFailureKind.Timeout;
            }
        }

        return null;
    }
}

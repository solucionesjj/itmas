using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Itmas3.Agent.Collectors.SqlAudit;
using Itmas3.Agent.Collectors.SqlAudit.Model;
using Itmas3.Agent.SqlAudit;

namespace Itmas3.Agent.Cli;

/// <summary>Obtains the certificate a SQL Server presents (to pin it by SHA-256): host, port, named instance.</summary>
public delegate Task<X509Certificate2> SqlServerCertificateSource(string host, int? port, string? instanceName, CancellationToken cancellationToken);

/// <summary>
/// <c>itmas-agent sql add | update &lt;alias&gt; | list | test &lt;alias&gt; | remove &lt;alias&gt;</c> (SPEC-0005 RF-2, RF-3).
/// <list type="bullet">
/// <item>The password is read hidden, from <c>--password-file</c> or from <c>--password-env</c>; never from an argument.</item>
/// <item><c>add</c>/<c>update</c> test the connection, the certificate and the catalog read before saving, and save
/// nothing if the test fails; the cause is explained (DNS, connection, engine without TLS, certificate, credentials,
/// permissions).</item>
/// <item>The TLS mode is never changed by the agent: <c>unverified</c> requires typing <see cref="RiskConfirmation"/>
/// (or <c>--tls unverified</c> without a console), and a failed <c>verify</c> is never retried in another mode.</item>
/// <item>Connection strings are always composed by the drivers' builders from validated fields.</item>
/// </list>
/// </summary>
public sealed class SqlCommand(
    IAgentConsole console,
    ISqlConnectionStore store,
    Func<string, ISqlEngine> engines,
    SqlServerCertificateSource sqlServerCertificates,
    Func<string, string?> environment,
    TimeProvider timeProvider)
{
    public const string RiskConfirmation = "ACEPTO EL RIESGO";
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromMinutes(2);

    private static readonly HashSet<string> ValueOptions = new(StringComparer.Ordinal)
    {
        "--alias", "--engine", "--host", "--port", "--instance", "--database", "--user", "--password-file", "--password-env",
        "--tls", "--certificate-file", "--certificate-sha256",
    };

    public async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 0)
        {
            console.WriteLine(Usage);
            return 2;
        }

        if (Parse(arguments.Skip(1).ToList()) is not { } parsed)
        {
            return 2;
        }

        var (positional, options) = parsed;
        try
        {
            return arguments[0].ToLowerInvariant() switch
            {
                "add" when positional.Count == 0 => await AddAsync(options, cancellationToken),
                "update" when positional.Count == 1 => await UpdateAsync(positional[0], options, cancellationToken),
                "list" when positional.Count == 0 && options.Count == 0 => List(),
                "test" when positional.Count == 1 && options.Count == 0 => await TestAsync(positional[0], cancellationToken),
                "remove" when positional.Count == 1 && options.Count == 0 => Remove(positional[0]),
                _ => UsageError(),
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or System.Text.Json.JsonException)
        {
            console.WriteError($"No se pudo leer o guardar la configuración de conexiones SQL: {exception.GetType().Name}.");
            return 5;
        }
    }

    public static string Usage => """
        Uso:
          itmas-agent sql add [opciones]             Añade una conexión (prueba la conexión antes de guardarla).
          itmas-agent sql update <alias> [opciones]  Cambia una conexión, por ejemplo para rotar la contraseña.
          itmas-agent sql list                       Lista las conexiones y el resultado de la última recolección.
          itmas-agent sql test <alias>               Prueba la conexión y avisa si la cuenta tiene privilegios de más.
          itmas-agent sql remove <alias>             Borra la conexión y su contraseña.

        Opciones de add y update (sin ellas se preguntan en la consola):
          --alias <alias>  --engine sqlserver|postgresql|mysql|mariadb  --host <host>  --port <puerto>
          --instance <instancia con nombre de SQL Server>  --database <base inicial de PostgreSQL>  --user <usuario>
          --password-file <ruta> | --password-env <VARIABLE>   (la contraseña nunca se acepta como argumento)
          --tls verify|pinned|unverified   --certificate-file <ruta> | --certificate-sha256 <huella>   (modo pinned)

        Requieren privilegios de administrador (sudo en Linux y macOS).
        """;

    private int UsageError()
    {
        console.WriteError(Usage);
        return 2;
    }

    // ---- add / update ----------------------------------------------------------------------------------------------

    private async Task<int> AddAsync(Dictionary<string, string> options, CancellationToken cancellationToken)
    {
        var existing = store.List();
        if (existing.Count >= SqlConnectionSettings.MaxConnections)
        {
            console.WriteError($"Ya hay {SqlConnectionSettings.MaxConnections} conexiones configuradas, el máximo por agente. Borre alguna con 'itmas-agent sql remove'.");
            return 2;
        }

        var alias = Value(options, "--alias", "Alias de la conexión (por ejemplo, ventas-prod)", null);
        if (alias is null || !SqlConnectionRules.IsValidAlias(alias))
        {
            return Invalid("El alias debe empezar por una letra o un número y tener como máximo 64 caracteres (letras, números, '.', '_' o '-').");
        }

        if (existing.Any(connection => string.Equals(connection.Alias, alias, StringComparison.OrdinalIgnoreCase)))
        {
            return Invalid($"Ya existe una conexión con el alias {alias}. Use 'itmas-agent sql update {alias}' para cambiarla.");
        }

        var engine = Value(options, "--engine", "Motor (sqlserver, postgresql, mysql o mariadb)", null)?.ToLowerInvariant();
        if (!SqlConnectionRules.IsValidEngine(engine))
        {
            return Invalid("El motor debe ser sqlserver, postgresql, mysql o mariadb.");
        }

        var draft = new SqlConnectionDefinition(Guid.NewGuid(), alias, engine!, string.Empty, null, null, null, string.Empty, SqlTlsModes.Verify, null, default, default);
        return await EditAsync(draft, isNew: true, options, cancellationToken);
    }

    private async Task<int> UpdateAsync(string alias, Dictionary<string, string> options, CancellationToken cancellationToken)
    {
        if (store.Find(alias) is not { } current)
        {
            return Invalid($"No existe ninguna conexión con el alias {alias}. Consulte 'itmas-agent sql list'.");
        }

        if (options.ContainsKey("--alias"))
        {
            return Invalid("El alias no se puede cambiar: borre la conexión y créela de nuevo.");
        }

        var engine = options.TryGetValue("--engine", out var givenEngine) ? givenEngine.ToLowerInvariant() : current.Engine;
        if (!SqlConnectionRules.IsValidEngine(engine))
        {
            return Invalid("El motor debe ser sqlserver, postgresql, mysql o mariadb.");
        }

        return await EditAsync(current with { Engine = engine }, isNew: false, options, cancellationToken);
    }

    private async Task<int> EditAsync(SqlConnectionDefinition current, bool isNew, Dictionary<string, string> options, CancellationToken cancellationToken)
    {
        var engine = current.Engine;
        var host = Value(options, "--host", "Host del motor (nombre DNS o dirección IP)", NullIfEmpty(current.Host));
        if (!SqlConnectionRules.IsValidHost(host))
        {
            return Invalid("El host debe ser un nombre DNS o una dirección IP, sin puerto ni instancia.");
        }

        string? instance = null;
        int? port;
        if (engine == SqlEngines.SqlServer)
        {
            instance = options.TryGetValue("--instance", out var givenInstance) ? givenInstance
                : console.IsInteractive && !options.ContainsKey("--port") ? Ask("Instancia con nombre (Enter si no tiene)", current.InstanceName, allowEmpty: true)
                : current.InstanceName;
            if (instance is not null && !SqlConnectionRules.IsValidInstanceName(instance))
            {
                return Invalid("La instancia con nombre admite hasta 16 letras, números, '_', '$', '#' o '&'.");
            }
        }
        else if (options.ContainsKey("--instance"))
        {
            return Invalid("La instancia con nombre solo se admite en SQL Server.");
        }

        var defaultPort = instance is null ? (current.Port ?? SqlConnectionSettings.DefaultPort(engine)).ToString(CultureInfo.InvariantCulture) : current.Port?.ToString(CultureInfo.InvariantCulture);
        var portText = Value(options, "--port", instance is null ? "Puerto" : "Puerto (Enter para resolverlo con SQL Browser)", defaultPort, allowEmpty: instance is not null);
        if (string.IsNullOrEmpty(portText))
        {
            port = null;
        }
        else if (int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort) && SqlConnectionRules.IsValidPort(parsedPort))
        {
            port = parsedPort;
        }
        else
        {
            return Invalid("El puerto debe ser un número entre 1 y 65535.");
        }

        string? database = null;
        if (engine == SqlEngines.PostgreSql)
        {
            database = Value(options, "--database", "Base de datos inicial", current.Database ?? SqlConnectionSettings.DefaultPostgreSqlDatabase);
            if (!SqlConnectionRules.IsValidName(database, 63))
            {
                return Invalid("La base de datos inicial debe ser un nombre de hasta 63 caracteres.");
            }
        }
        else if (options.ContainsKey("--database"))
        {
            return Invalid("La base de datos inicial solo se indica en PostgreSQL.");
        }

        var user = Value(options, "--user", "Usuario del motor dedicado al agente", NullIfEmpty(current.User));
        if (!SqlConnectionRules.IsValidName(user))
        {
            return Invalid("Indique el usuario del motor dedicado al agente (--user).");
        }

        if (await ReadPasswordAsync(options, required: isNew, cancellationToken) is not { } password)
        {
            return 2;
        }

        var tls = Value(options, "--tls", "Modo TLS: verify (recomendado), pinned o unverified", current.Tls)?.ToLowerInvariant();
        if (!SqlConnectionRules.IsValidTlsMode(tls))
        {
            return Invalid("El modo TLS debe ser verify, pinned o unverified.");
        }

        var updated = current with
        {
            Host = host!,
            Port = port,
            InstanceName = instance,
            Database = database,
            User = user!,
            Tls = tls!,
            PinnedSha256 = tls == SqlTlsModes.Pinned ? current.PinnedSha256 : null,
        };

        var newCertificate = options.ContainsKey("--certificate-file") || options.ContainsKey("--certificate-sha256")
            || (tls == SqlTlsModes.Pinned && (current.Tls != SqlTlsModes.Pinned || current.PinnedSha256 is null || engine != current.Engine || isNew));
        if (tls != SqlTlsModes.Pinned && newCertificate)
        {
            return Invalid("--certificate-file y --certificate-sha256 solo se usan con --tls pinned.");
        }

        if (tls == SqlTlsModes.Unverified && current.Tls != SqlTlsModes.Unverified && !ConfirmUnverified())
        {
            console.WriteLine("No se activó el modo unverified. No se guardó ningún cambio.");
            return 3;
        }

        try
        {
            if (tls == SqlTlsModes.Pinned && newCertificate)
            {
                if (await PinCertificateAsync(updated, options, cancellationToken) is not { } pinned)
                {
                    return 2;
                }

                updated = updated with { PinnedSha256 = pinned };
            }

            var secret = password.Value ?? store.LoadPassword(current);
            if (string.IsNullOrEmpty(secret))
            {
                return Invalid("No hay contraseña guardada para esta conexión: indíquela con --password-file o --password-env.");
            }

            var settings = store.Settings(updated, pendingCertificate: tls == SqlTlsModes.Pinned && newCertificate);
            if (SqlConnectionRules.Validate(settings) is { } problem)
            {
                return Invalid(problem);
            }

            console.WriteLine($"Probando la conexión con {settings.Destination} ({engine}, TLS {tls}) …");
            if (!await ProbeAsync(settings, secret, cancellationToken))
            {
                store.DiscardPendingCertificate(updated.Id);
                console.WriteError("No se guardó ningún cambio.");
                return 4;
            }

            var now = timeProvider.GetUtcNow();
            store.Save(updated with { CreatedAt = isNew ? now : current.CreatedAt, UpdatedAt = now }, password.Value);
        }
        catch
        {
            store.DiscardPendingCertificate(updated.Id);
            throw;
        }

        console.WriteLine(isNew
            ? $"Conexión {updated.Alias} guardada. El agente la auditará cada día junto con el inventario."
            : $"Conexión {updated.Alias} actualizada.");
        if (tls == SqlTlsModes.Unverified)
        {
            console.WriteLine("Recuerde: esta conexión no autentica al servidor y ITMas la señalará con un aviso. Pase a pinned o verify en cuanto sea posible.");
        }

        return 0;
    }

    /// <summary>Password from a file, an environment variable or the hidden prompt. Value null keeps the stored one.</summary>
    private async Task<(string? Value, bool Given)?> ReadPasswordAsync(Dictionary<string, string> options, bool required, CancellationToken cancellationToken)
    {
        if (options.TryGetValue("--password-file", out var file))
        {
            if (!File.Exists(file))
            {
                console.WriteError("No existe el archivo indicado en --password-file.");
                return null;
            }

            var fromFile = (await File.ReadAllTextAsync(file, cancellationToken)).TrimEnd('\r', '\n');
            return fromFile.Length > 0 ? (fromFile, true) : Missing("El archivo de --password-file está vacío.");
        }

        if (options.TryGetValue("--password-env", out var variable))
        {
            return environment(variable) is { Length: > 0 } fromEnvironment
                ? (fromEnvironment, true)
                : Missing($"La variable de entorno {variable} no existe o está vacía.");
        }

        if (!console.IsInteractive)
        {
            return required ? Missing("Indique la contraseña con --password-file o --password-env (nunca como argumento).") : (null, false);
        }

        console.WriteLine(required
            ? "Contraseña de la cuenta del agente (no se mostrará al escribirla):"
            : "Nueva contraseña (no se mostrará al escribirla; Enter para mantener la actual):");
        var typed = console.ReadSecret();
        if (string.IsNullOrEmpty(typed))
        {
            return required ? Missing("No se indicó la contraseña.") : (null, false);
        }

        return (typed, true);

        (string?, bool)? Missing(string message)
        {
            console.WriteError(message);
            return null;
        }
    }

    /// <summary>ADR-0015: explains the risk, proposes pinned and requires the exact confirmation; never automatic.</summary>
    private bool ConfirmUnverified()
    {
        if (!console.IsInteractive)
        {
            console.WriteLine("Aviso: modo unverified elegido con --tls unverified. La conexión irá cifrada, pero el agente no autenticará al servidor.");
            return true;
        }

        console.WriteLine("""

            ATENCIÓN: en el modo unverified la conexión va cifrada, pero el agente NO comprueba el certificado del servidor.
            Un atacante en la red podría suplantar el motor, enviar datos falsos y, según el método de autenticación,
            capturar la contraseña de la cuenta del agente. ITMas señalará esta instancia con un aviso.

            Alternativa recomendada para certificados autofirmados: el modo pinned, que acepta solo el certificado que
            le indique (archivo o huella SHA-256, que puede facilitarle el DBA):
              itmas-agent sql add … --tls pinned --certificate-file <ruta>
            """);
        console.WriteLine($"Para usar igualmente el modo unverified en esta conexión escriba {RiskConfirmation} (cualquier otra respuesta cancela):");
        return string.Equals(console.ReadLine()?.Trim(), RiskConfirmation, StringComparison.Ordinal);
    }

    /// <summary>Returns the SHA-256 to pin and, for SQL Server, stages the certificate file SqlClient compares with.</summary>
    private async Task<string?> PinCertificateAsync(SqlConnectionDefinition connection, Dictionary<string, string> options, CancellationToken cancellationToken)
    {
        string? file = options.GetValueOrDefault("--certificate-file");
        string? fingerprint = options.GetValueOrDefault("--certificate-sha256");
        if (file is null && fingerprint is null)
        {
            if (!console.IsInteractive)
            {
                console.WriteError("El modo pinned requiere --certificate-file <ruta> o --certificate-sha256 <huella>.");
                return null;
            }

            var answer = Ask("Certificado del servidor: ruta del archivo (PEM o DER) o huella SHA-256", null);
            if (SqlTls.NormalizeThumbprint(answer) is not null)
            {
                fingerprint = answer;
            }
            else
            {
                file = answer;
            }
        }

        byte[] der;
        string thumbprint;
        if (file is not null)
        {
            try
            {
                using var certificate = SqlTls.LoadCertificate(file);
                der = certificate.RawData;
                thumbprint = SqlTls.Thumbprint(certificate);
            }
            catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException)
            {
                console.WriteError("No se pudo leer el certificado: el archivo no existe o no es un certificado PEM o DER.");
                return null;
            }
        }
        else
        {
            if (SqlTls.NormalizeThumbprint(fingerprint) is not { } normalized)
            {
                console.WriteError("La huella debe ser un SHA-256 en hexadecimal (64 caracteres; se admiten ':' y espacios).");
                return null;
            }

            thumbprint = normalized;
            if (connection.Engine != SqlEngines.SqlServer)
            {
                console.WriteLine($"Certificado fijado: SHA-256 {Format(thumbprint)}");
                return thumbprint;
            }

            // SqlClient pins by certificate file: obtain the certificate the server presents and check the fingerprint.
            try
            {
                using var presented = await sqlServerCertificates(connection.Host, connection.Port, connection.InstanceName, cancellationToken);
                var presentedThumbprint = SqlTls.Thumbprint(presented);
                if (!string.Equals(presentedThumbprint, thumbprint, StringComparison.Ordinal))
                {
                    console.WriteError($"El servidor presenta otro certificado (SHA-256 {Format(presentedThumbprint)}). No coincide con la huella indicada; no se guardó ningún cambio.");
                    return null;
                }

                der = presented.RawData;
            }
            catch (Exception exception) when (exception is SqlAuditException or System.Net.Sockets.SocketException or IOException
                or System.Security.Authentication.AuthenticationException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                var kind = exception is SqlAuditException audit ? audit.Kind : SqlFailureKind.Connection;
                console.WriteError($"No se pudo obtener el certificado del servidor: {SqlFailures.Explain(kind)} También puede indicarlo con --certificate-file.");
                return null;
            }
        }

        if (connection.Engine == SqlEngines.SqlServer)
        {
            store.StagePinnedCertificate(connection.Id, der);
        }

        console.WriteLine($"Certificado fijado: SHA-256 {Format(thumbprint)}");
        return thumbprint;
    }

    private async Task<bool> ProbeAsync(SqlConnectionSettings settings, string password, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);
        SqlProbeResult result;
        try
        {
            result = await engines(settings.Engine).ProbeAsync(settings, password, timeout.Token);
        }
        catch (Exception exception) when (exception is SqlAuditException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            var kind = exception is SqlAuditException audit ? audit.Kind : SqlFailureKind.Timeout;
            console.WriteError($"No se pudo verificar la conexión: {SqlFailures.Explain(kind)}");
            if (kind == SqlFailureKind.Certificate && settings.TlsMode == SqlTlsModes.Verify)
            {
                console.WriteError("El agente no cambia de modo TLS por su cuenta. Si el motor usa un certificado autofirmado, fíjelo con "
                    + "--tls pinned --certificate-file <ruta> (o --certificate-sha256 <huella>). Como último recurso existe --tls unverified, "
                    + "que no autentica al servidor y requiere confirmación expresa.");
            }

            return false;
        }

        console.WriteLine($"Conexión verificada: {settings.Engine} {result.Version}{(result.Edition is null ? string.Empty : $" ({result.Edition})")}, "
            + $"{result.Accounts} cuentas y {result.Databases} bases de datos legibles.");
        if (result.ExcessPrivileges.Count > 0)
        {
            console.WriteLine("Aviso: la cuenta del agente tiene más privilegios de los necesarios para leer el catálogo:");
            foreach (var privilege in result.ExcessPrivileges)
            {
                console.WriteLine($"  - {privilege}");
            }

            console.WriteLine("Se recomienda usar la cuenta de solo lectura descrita en el manual (preparar-motor-sql).");
        }

        return true;
    }

    // ---- list / test / remove --------------------------------------------------------------------------------------

    private int List()
    {
        var connections = store.List();
        if (connections.Count == 0)
        {
            console.WriteLine("No hay conexiones SQL configuradas. Añada una con 'itmas-agent sql add'.");
            return 0;
        }

        var results = store.LoadResults();
        console.WriteLine($"{"ALIAS",-24} {"MOTOR",-10} {"DESTINO",-36} {"USUARIO",-20} {"TLS",-10} ÚLTIMA RECOLECCIÓN");
        foreach (var connection in connections)
        {
            var settings = SqlConnectionStore.ToSettings(connection, string.Empty);
            console.WriteLine($"{connection.Alias,-24} {connection.Engine,-10} {Destination(settings),-36} {connection.User,-20} {connection.Tls,-10} {LastResult(results.GetValueOrDefault(connection.Id))}");
        }

        return 0;
    }

    private async Task<int> TestAsync(string alias, CancellationToken cancellationToken)
    {
        if (store.Find(alias) is not { } connection)
        {
            return Invalid($"No existe ninguna conexión con el alias {alias}. Consulte 'itmas-agent sql list'.");
        }

        if (store.LoadPassword(connection) is not { Length: > 0 } password)
        {
            return Invalid($"Falta la contraseña de la conexión {alias}. Indíquela con 'itmas-agent sql update {alias}'.");
        }

        var settings = store.Settings(connection);
        console.WriteLine($"Probando la conexión {alias} con {settings.Destination} ({connection.Engine}, TLS {connection.Tls}) …");
        return await ProbeAsync(settings, password, cancellationToken) ? 0 : 4;
    }

    private int Remove(string alias)
    {
        if (!store.Remove(alias))
        {
            return Invalid($"No existe ninguna conexión con el alias {alias}. Consulte 'itmas-agent sql list'.");
        }

        console.WriteLine($"Conexión {alias} borrada junto con su contraseña. Lo ya enviado se conserva en ITMas.");
        return 0;
    }

    // ---- helpers ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Splits options and positional arguments. Secrets are never accepted as arguments (constitution §4.5): they would
    /// end up in the shell history and the process list. Free-form connection strings do not exist (ADR-0014 §3).
    /// </summary>
    private (List<string> Positional, Dictionary<string, string> Options)? Parse(List<string> arguments)
    {
        var positional = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            var name = argument.Split('=', 2)[0].ToLowerInvariant();
            if (name is "--pwd" or "-p" or "--secret" || (name.StartsWith("--pass", StringComparison.Ordinal) && !ValueOptions.Contains(name)))
            {
                console.WriteError("Por seguridad la contraseña no se acepta como argumento. Use la solicitud interactiva, --password-file <ruta> o --password-env <VARIABLE>.");
                return null;
            }

            if (name is "--connection-string" or "--connectionstring" or "--cs")
            {
                console.WriteError("No se admiten cadenas de conexión: el agente compone la conexión a partir de --host, --port, --user y --tls.");
                return null;
            }

            if (!argument.StartsWith('-'))
            {
                positional.Add(argument);
                continue;
            }

            if (!ValueOptions.Contains(argument) || index + 1 >= arguments.Count)
            {
                console.WriteError($"Opción no reconocida o sin valor: {argument}");
                return null;
            }

            options[argument] = arguments[++index];
        }

        return (positional, options);
    }

    private string? Value(Dictionary<string, string> options, string option, string prompt, string? current, bool allowEmpty = false)
    {
        if (options.TryGetValue(option, out var given))
        {
            return given.Trim();
        }

        return console.IsInteractive ? Ask(prompt, current, allowEmpty) : current;
    }

    private string? Ask(string prompt, string? current, bool allowEmpty = false)
    {
        console.WriteLine(current is null ? $"{prompt}:" : $"{prompt} [{current}] (Enter para aceptar):");
        var answer = console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(answer) ? (allowEmpty && current is null ? null : current) : answer;
    }

    private int Invalid(string message)
    {
        console.WriteError(message);
        return 2;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static string Format(string thumbprint) =>
        string.Join(':', Enumerable.Range(0, thumbprint.Length / 2).Select(index => thumbprint.Substring(index * 2, 2)));

    private static string Destination(SqlConnectionSettings settings) =>
        settings.Database is null ? settings.Destination : $"{settings.Destination}/{settings.Database}";

    private static string LastResult(SqlConnectionResult? result)
    {
        if (result?.LastAttemptAt is not { } attempt)
        {
            return "pendiente";
        }

        var at = attempt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return result.LastOutcome switch
        {
            SqlOutcomes.Complete => $"{at} correcta",
            SqlOutcomes.Partial => $"{at} parcial ({result.UnavailableSections} partes no disponibles)",
            SqlOutcomes.Unreachable => $"{at} motor no disponible: {result.LastError}",
            _ => $"{at} fallida: {result.LastError}{(result.NextRetryAt is { } retry ? $" (reintento {retry.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)})" : string.Empty)}",
        };
    }
}

using System.Data.Common;
using System.Globalization;

namespace Itmas3.Agent.Collectors.SqlAudit;

/// <summary>
/// Read-only execution of the fixed catalog queries (constitution §13.6): the SQL text is always a constant of the engine's
/// query class, there are no parameters built from data and every command has the 30 s limit (RNF-1). Rows are copied
/// into plain records so the mappers stay pure and testable with fixtures.
/// </summary>
internal static class DbRows
{
    public static async Task<List<T>> QueryAsync<T>(DbConnection connection, string fixedSql, Func<DbDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Fixed catalog query text (constants of the *Queries classes); nothing is concatenated (ADR-0014 §6).
        command.CommandText = fixedSql;
#pragma warning restore CA2100
        command.CommandTimeout = (int)SqlConnectionSettings.QueryTimeout.TotalSeconds;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    public static async Task ExecuteAsync(DbConnection connection, string fixedSql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Fixed session settings statement; nothing is concatenated.
        command.CommandText = fixedSql;
#pragma warning restore CA2100
        command.CommandTimeout = (int)SqlConnectionSettings.QueryTimeout.TotalSeconds;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static string? String(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetValue(ordinal) switch
        {
            string text => text,
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            char character => character.ToString(),
            var value => Convert.ToString(value, CultureInfo.InvariantCulture),
        };
    }

    public static string RequiredString(DbDataReader reader, string column) => String(reader, column) ?? string.Empty;

    public static long? Long(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    /// <summary>Unsigned 64-bit values such as MariaDB privilege masks (which may have every bit set).</summary>
    public static ulong? ULong(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToUInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    public static int? Int(DbDataReader reader, string column) => Long(reader, column) is { } value ? (int)value : null;

    /// <summary>Booleans as the engines return them: bit, boolean, numbers or 'Y'/'N'.</summary>
    public static bool? Bool(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetValue(ordinal) switch
        {
            bool flag => flag,
            string text => text.Trim() switch { "Y" or "y" or "1" or "true" or "t" => true, "N" or "n" or "0" or "false" or "f" or "" => false, _ => null },
            var value => Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0,
        };
    }

    public static DateTime? DateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetValue(ordinal) switch
            {
                System.DateTime value => value,
                DateTimeOffset value => value.UtcDateTime,
                _ => null,
            };
    }

    /// <summary>Binary identifiers (e.g. SQL Server SIDs) as upper-case hexadecimal, only to join principals.</summary>
    public static string? Hex(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) || reader.GetValue(ordinal) is not byte[] bytes ? null : Convert.ToHexString(bytes);
    }
}

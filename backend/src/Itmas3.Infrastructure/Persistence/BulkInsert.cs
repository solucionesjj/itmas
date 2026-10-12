using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace Itmas3.Infrastructure.Persistence;

/// <summary>
/// Inserts many rows of an entity with PostgreSQL binary <c>COPY</c>, inside the current transaction (SPEC-0005 RNF-2:
/// a first snapshot may add 200 000 elements). The command text is built only from the EF Core model (quoted table and
/// column names), never from data; values travel in the binary stream after applying each property's value converter.
/// The entities are not tracked: they must have no store-generated values.
/// </summary>
internal static class BulkInsert
{
    public static async Task CopyAsync<T>(AppDbContext db, IReadOnlyCollection<T> entities, CancellationToken cancellationToken)
        where T : class
    {
        if (entities.Count == 0)
        {
            return;
        }

        var entityType = db.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException($"{typeof(T).Name} is not mapped.");
        var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
        var columns = entityType.GetProperties()
            .Where(property => property.PropertyInfo is not null && property.GetColumnName(table) is not null)
            .Select(property => new Column(property, property.GetColumnName(table)!, BaseType(property.GetColumnType(table))))
            .ToList();

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var command = $"COPY {Quote(table.Schema!)}.{Quote(table.Name)} ({string.Join(", ", columns.Select(column => Quote(column.Name)))}) FROM STDIN (FORMAT BINARY)";
        await using var importer = await connection.BeginBinaryImportAsync(command, cancellationToken);
        foreach (var entity in entities)
        {
            await importer.StartRowAsync(cancellationToken);
            foreach (var column in columns)
            {
                var value = column.Property.PropertyInfo!.GetValue(entity);
                if (value is not null && column.Property.GetTypeMapping().Converter is { } converter)
                {
                    value = converter.ConvertToProvider(value);
                }

                if (value is null)
                {
                    await importer.WriteNullAsync(cancellationToken);
                }
                else
                {
                    await importer.WriteAsync(value, column.DataTypeName, cancellationToken);
                }
            }
        }

        await importer.CompleteAsync(cancellationToken);
    }

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    /// <summary>Data type without its modifier (<c>character varying(256)</c> → <c>character varying</c>).</summary>
    private static string BaseType(string columnType) =>
        columnType.IndexOf('(', StringComparison.Ordinal) is var index and >= 0 ? columnType[..index].TrimEnd() : columnType;

    private sealed record Column(IProperty Property, string Name, string DataTypeName);
}

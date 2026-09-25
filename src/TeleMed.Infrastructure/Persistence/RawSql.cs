using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace TeleMed.Infrastructure.Persistence;

// Read-model queries that bucket by time zone or union tables are plainer as SQL than as LINQ; this runs them on the context's connection.
internal static class RawSql
{
    public static async IAsyncEnumerable<T> StreamAsync<T>(
        this AppDbContext db, string sql, Func<NpgsqlParameter[]> parameters, Func<DbDataReader, T> map, [EnumeratorCancellation] CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened)
        {
            await db.Database.OpenConnectionAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.Parameters.AddRange(parameters());
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                yield return map(reader);
            }
        }
        finally
        {
            if (opened)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }

    public static async Task<List<T>> QueryAsync<T>(this AppDbContext db, string sql, Func<NpgsqlParameter[]> parameters, Func<DbDataReader, T> map, CancellationToken ct)
    {
        var rows = new List<T>();
        await foreach (var row in db.StreamAsync(sql, parameters, map, ct))
        {
            rows.Add(row);
        }

        return rows;
    }

    public static NpgsqlParameter Param(string name, object value) => new(name, value);

    public static NpgsqlParameter Uuid(string name, Guid? value) => new(name, NpgsqlDbType.Uuid) { Value = (object?)value ?? DBNull.Value };

    public static T? NullableEnum<T>(this DbDataReader reader, int ordinal)
        where T : struct, Enum =>
        reader.IsDBNull(ordinal) ? null : Enum.Parse<T>(reader.GetString(ordinal).Replace("_", "", StringComparison.Ordinal), ignoreCase: true);

    public static string? NullableString(this DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static DateOnly Date(this DbDataReader reader, int ordinal) => reader.GetFieldValue<DateOnly>(ordinal);

    public static DateTimeOffset Instant(this DbDataReader reader, int ordinal) => reader.GetFieldValue<DateTimeOffset>(ordinal);
}

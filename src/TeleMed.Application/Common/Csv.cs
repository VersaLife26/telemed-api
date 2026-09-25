using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TeleMed.Application.Common;

public static class Csv
{
    public const string ContentType = "text/csv";

    public static async Task WriteAsync<T>(Stream output, IReadOnlyList<string> header, IAsyncEnumerable<T> rows, Func<T, IEnumerable<object?>> fields, CancellationToken ct)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteLineAsync(Line(header));
        await foreach (var row in rows.WithCancellation(ct))
        {
            await writer.WriteLineAsync(Line(fields(row)));
        }

        await writer.FlushAsync(ct);
    }

    private static string Line(IEnumerable<object?> values) => string.Join(',', values.Select(Field));

    // Leading =, +, - and @ are prefixed so spreadsheet apps do not evaluate user-supplied text as a formula.
    private static string Field(object? value)
    {
        var text = value switch
        {
            null => "",
            DateTimeOffset instant => instant.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            Enum member => JsonNamingPolicy.CamelCase.ConvertName(member.ToString()),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
        if (value is string && text.Length > 0 && text[0] is '=' or '+' or '-' or '@')
        {
            text = "'" + text;
        }

        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : text;
    }
}

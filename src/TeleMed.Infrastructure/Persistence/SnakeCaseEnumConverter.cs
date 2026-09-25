using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TeleMed.Infrastructure.Persistence;

internal sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(v => ToDb[v], v => FromDb[v])
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> ToDb = Enum.GetValues<TEnum>()
        .ToDictionary(v => v, v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()));

    private static readonly Dictionary<string, TEnum> FromDb = ToDb.ToDictionary(p => p.Value, p => p.Key);
}

internal static class SnakeCaseEnumModelBuilderExtensions
{
    public static void UseSnakeCaseEnumStrings(this ModelBuilder builder)
    {
        foreach (var property in builder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (type.IsEnum && property.GetValueConverter() is null)
            {
                property.SetValueConverter((ValueConverter)Activator.CreateInstance(typeof(SnakeCaseEnumConverter<>).MakeGenericType(type))!);
            }
        }
    }
}

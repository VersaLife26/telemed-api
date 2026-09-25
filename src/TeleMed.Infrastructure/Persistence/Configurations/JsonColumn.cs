using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TeleMed.Infrastructure.Persistence.Configurations;

// Stored as a scalar jsonb value rather than an owned type so the AuditInterceptor sees one property with old/new values.
internal static class JsonColumn
{
    private static readonly JsonSerializerOptions Options = JsonSerializerOptions.Web;

    public static PropertyBuilder<T> HasJsonbConversion<T>(this PropertyBuilder<T> property) =>
        property
            .HasConversion(
                v => JsonSerializer.Serialize(v, Options),
                v => JsonSerializer.Deserialize<T>(v, Options)!,
                new ValueComparer<T>(
                    (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                    v => JsonSerializer.Serialize(v, Options).GetHashCode(StringComparison.Ordinal),
                    v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!))
            .HasColumnType("jsonb");
}

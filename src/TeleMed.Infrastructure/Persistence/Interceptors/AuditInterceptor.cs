using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Common;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Persistence.Interceptors;

internal sealed class AuditInterceptor(ICurrentActor actor, IRequestContext request, TimeProvider time) : SaveChangesInterceptor
{
    private const int MaxUserAgentLength = 512;

    // Identity's inherited columns cannot carry [AuditIgnore], and timestamps are noise.
    private static readonly HashSet<string> IgnoredProperties =
    [
        nameof(ITimestamped.CreatedAt),
        nameof(ITimestamped.UpdatedAt),
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
    ];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Record(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Record(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var logs = context.ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditable && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(ToLog)
            .OfType<AuditLog>()
            .ToList();
        context.AddRange(logs);
    }

    private AuditLog? ToLog(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>();
        foreach (var property in entry.Properties.Where(IsAudited))
        {
            var column = property.Metadata.GetColumnName();
            switch (entry.State)
            {
                case EntityState.Added:
                    changes[column] = new { New = Provider(property, property.CurrentValue) };
                    break;
                case EntityState.Deleted:
                    changes[column] = new { Old = Provider(property, property.OriginalValue) };
                    break;
                case EntityState.Modified when property.IsModified && !Equals(property.OriginalValue, property.CurrentValue):
                    changes[column] = new { Old = Provider(property, property.OriginalValue), New = Provider(property, property.CurrentValue) };
                    break;
            }
        }

        if (entry.State == EntityState.Modified && changes.Count == 0)
        {
            return null;
        }

        var admin = actor.Admin;
        var userId = actor.UserId;
        return new AuditLog
        {
            ActorType = admin is not null ? AuditActorType.Admin : userId is not null ? AuditActorType.User : AuditActorType.System,
            ActorId = admin?.Id ?? userId,
            ActorEmail = admin?.Email,
            Action = entry.State switch
            {
                EntityState.Added => "created",
                EntityState.Deleted => "deleted",
                _ => "updated",
            },
            EntityType = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
            EntityId = string.Join(",", entry.Metadata.FindPrimaryKey()!.Properties.Select(p => entry.Property(p.Name).CurrentValue)),
            Changes = JsonSerializer.Serialize(changes, JsonSerializerOptions.Web),
            Ip = request.IpAddress,
            UserAgent = request.UserAgent is { Length: > MaxUserAgentLength } ua ? ua[..MaxUserAgentLength] : request.UserAgent,
            RequestId = request.RequestId,
            CreatedAt = time.GetUtcNow(),
        };
    }

    // Store-generated columns (xmin, computed search vectors) are not changes anyone made.
    private static bool IsAudited(PropertyEntry property) =>
        !IgnoredProperties.Contains(property.Metadata.Name)
        && property.Metadata.ValueGenerated != ValueGenerated.OnAddOrUpdate
        && property.Metadata.PropertyInfo?.GetCustomAttribute<AuditIgnoreAttribute>() is null;

    private static object? Provider(PropertyEntry property, object? value)
    {
        if (value is null)
        {
            return null;
        }

        var stored = property.Metadata.GetTypeMapping().Converter?.ConvertToProvider(value) ?? value;
        return stored is string json && property.Metadata.GetColumnType() == "jsonb" ? JsonDocument.Parse(json).RootElement : stored;
    }
}

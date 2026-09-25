using System.Text.Json;
using TeleMed.Application.Common;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Audit;

public sealed record AuditEntryDto(
    long Id,
    AuditActorType ActorType,
    Guid? ActorId,
    string? ActorEmail,
    string Action,
    string EntityType,
    string EntityId,
    JsonElement Changes,
    string? Ip,
    string? RequestId,
    DateTimeOffset CreatedAt);

public interface IAuditQuery
{
    Guid? ActorId { get; }
    string? EntityType { get; }
    string? EntityId { get; }
    DateTimeOffset? From { get; }
    DateTimeOffset? To { get; }
}

public sealed record AuditQuery : PageQuery, IAuditQuery
{
    public Guid? ActorId { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public sealed record AuditExportQuery : IAuditQuery
{
    public Guid? ActorId { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

internal static class AuditMapper
{
    public static AuditEntryDto ToDto(this AuditLog log)
    {
        using var changes = JsonDocument.Parse(log.Changes);
        return new AuditEntryDto(
            log.Id,
            log.ActorType,
            log.ActorId,
            log.ActorEmail,
            log.Action,
            log.EntityType,
            log.EntityId,
            changes.RootElement.Clone(),
            log.Ip?.ToString(),
            log.RequestId,
            log.CreatedAt);
    }
}

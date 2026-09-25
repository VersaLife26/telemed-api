using System.Net;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class AuditLog
{
    public long Id { get; init; }
    public AuditActorType ActorType { get; init; }
    public Guid? ActorId { get; init; }
    public string? ActorEmail { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string Changes { get; init; }
    public IPAddress? Ip { get; init; }
    public string? UserAgent { get; init; }
    public string? RequestId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

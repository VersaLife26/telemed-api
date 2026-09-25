using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class ConsultationEvent
{
    public long Id { get; init; }
    public Guid ConsultationId { get; init; }
    public ConsultationEventKind Kind { get; init; }
    public UserRole? ActorRole { get; init; }
    public string? Data { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

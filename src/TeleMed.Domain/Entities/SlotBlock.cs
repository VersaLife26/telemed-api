using TeleMed.Domain.Common;

namespace TeleMed.Domain.Entities;

public class SlotBlock : Entity, IAuditable
{
    public Guid DoctorId { get; init; }
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
    public required string Reason { get; init; }
    public Guid CreatedByAdminId { get; init; }
}

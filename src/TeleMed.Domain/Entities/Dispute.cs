using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class Dispute : Entity, IAuditable
{
    public Guid? AppointmentId { get; init; }
    public Guid? PatientId { get; init; }
    public Guid? DoctorId { get; init; }
    public DisputeCategory Category { get; init; }
    public required string Subject { get; init; }
    public required string Description { get; init; }
    public DisputeStatus Status { get; set; } = DisputeStatus.Open;
    public Guid? OpenedByAdminId { get; init; }
    public Guid? OpenedByUserId { get; init; }
    public Guid? AssignedAdminId { get; set; }
    public string? Resolution { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedByAdminId { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public uint Version { get; init; }
}

using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class RescheduleRequest : Entity, IAuditable
{
    public Guid AppointmentId { get; init; }
    public Guid DoctorId { get; init; }
    public Guid PatientId { get; init; }
    public DateTimeOffset OriginalStartAt { get; init; }
    public DateTimeOffset OriginalEndAt { get; init; }
    public DateTimeOffset ProposedStartAt { get; init; }
    public DateTimeOffset ProposedEndAt { get; init; }
    public string? Reason { get; init; }
    public Guid RequestedByUserId { get; init; }
    public RescheduleStatus Status { get; set; } = RescheduleStatus.Pending;
    public DateTimeOffset? DecidedAt { get; set; }
    public CancellationActor? DecidedBy { get; set; }
    public Guid? DecidedById { get; set; }
    public uint Version { get; init; }

    public void Decide(RescheduleStatus outcome, CancellationActor by, Guid? byId, DateTimeOffset at)
    {
        Status = outcome;
        DecidedAt = at;
        DecidedBy = by;
        DecidedById = byId;
    }
}

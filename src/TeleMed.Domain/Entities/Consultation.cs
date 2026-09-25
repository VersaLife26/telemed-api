using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class Consultation : Entity
{
    public Guid AppointmentId { get; init; }
    public ConsultationStatus Status { get; set; } = ConsultationStatus.Scheduled;
    public DateTimeOffset? PatientWaitingSince { get; set; }
    public DateTimeOffset? AdmittedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public UserRole? EndedByRole { get; set; }
    public string? EndReason { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTimeOffset? DoctorJoinedAt { get; set; }
    public DateTimeOffset? PatientJoinedAt { get; set; }
    public DateTimeOffset? EarlyJoinOfferedAt { get; set; }
    public EarlyJoinResponse? EarlyJoinResponse { get; set; }
    public DateTimeOffset? EarlyJoinRespondedAt { get; set; }
    public DateTimeOffset? RunningLateNotifiedAt { get; set; }
    public uint Version { get; init; }

    public void End(ConsultationStatus outcome, UserRole? by, string reason, DateTimeOffset at)
    {
        Status = outcome;
        EndedAt = at;
        EndedByRole = by;
        EndReason = reason;
        DurationSeconds = (StartedAt ?? AdmittedAt) is { } from ? (int)Math.Max(0, (at - from).TotalSeconds) : null;
    }
}

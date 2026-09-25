using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Reschedules;

public sealed record ProposeRescheduleRequest(DateTimeOffset ProposedStartAt, string? Reason);

public sealed record RescheduleRequestDto(
    Guid Id,
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    DateTimeOffset OriginalStartAt,
    DateTimeOffset OriginalEndAt,
    DateTimeOffset ProposedStartAt,
    DateTimeOffset ProposedEndAt,
    string? Reason,
    RescheduleStatus Status,
    DateTimeOffset? DecidedAt,
    CancellationActor? DecidedBy,
    DateTimeOffset CreatedAt);

public sealed record RescheduleDecisionDto(RescheduleRequestDto Request, AppointmentDto Appointment);

public sealed record RescheduleQuery : PageQuery
{
    public RescheduleStatus? Status { get; init; }
}

public sealed record ExpiredReschedule(Guid RequestId, Guid DoctorId);

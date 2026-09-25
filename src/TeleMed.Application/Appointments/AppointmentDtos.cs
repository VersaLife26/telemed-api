using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Appointments;

public sealed record VisitPatientDto(string Name, DateOnly DateOfBirth, Sex? Sex, decimal? WeightKg, string? Allergies);

public sealed record IntakeDto(string Symptoms, string? VisitRelation);

public sealed record BookAppointmentRequest(Guid DoctorId, DateTimeOffset StartAt, VisitPatientDto VisitPatient, IntakeDto Intake);

public sealed record CancelAppointmentRequest(string? Reason);

public sealed record AppointmentDto(
    Guid Id,
    Guid PatientId,
    Guid DoctorId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    AppointmentStatus Status,
    long FeeCents,
    string Currency,
    VisitPatientDto VisitPatient,
    IntakeDto Intake,
    DateTimeOffset? PaymentDueAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? NoShowAt,
    DateTimeOffset? CancelledAt,
    CancellationActor? CancelledBy,
    string? CancellationReason,
    int? RefundPercent,
    bool IsTest,
    DateTimeOffset CreatedAt);

public sealed record LastVisitDetailsDto(string? Name, DateOnly? DateOfBirth, Sex? Sex, decimal? WeightKg, string? Allergies);

public sealed record AppointmentQuery : PageQuery
{
    public AppointmentStatus? Status { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public sealed record AppointmentFilter(
    Guid? PatientId, Guid? DoctorId, AppointmentStatus? Status, DateTimeOffset? From, DateTimeOffset? To, int Skip, int Take, bool IncludeTest = false);

public sealed record ExpiredBooking(Guid AppointmentId, Guid DoctorId);

public sealed record ReminderCandidate(Guid AppointmentId, Guid PatientId, string DoctorName, DateTimeOffset StartAt, DateTimeOffset? ConfirmedAt);

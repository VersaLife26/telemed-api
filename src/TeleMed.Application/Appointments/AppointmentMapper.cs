using TeleMed.Domain.Entities;

namespace TeleMed.Application.Appointments;

internal static class AppointmentMapper
{
    public static AppointmentDto ToDto(this Appointment a) => new(
        a.Id,
        a.PatientId,
        a.DoctorId,
        a.StartAt,
        a.EndAt,
        a.Status,
        a.FeeCents,
        a.Currency,
        a.ToVisitPatientDto(),
        new IntakeDto(a.Intake.Symptoms, a.Intake.VisitRelation),
        a.PaymentDueAt,
        a.ConfirmedAt,
        a.CompletedAt,
        a.NoShowAt,
        a.CancelledAt,
        a.CancelledBy,
        a.CancellationReason,
        a.RefundPercent,
        a.IsTest,
        a.CreatedAt);

    public static VisitPatientDto ToVisitPatientDto(this Appointment a) =>
        new(a.VisitPatientName, a.VisitPatientDateOfBirth, a.VisitPatientSex, a.VisitPatientWeightKg, a.VisitPatientAllergies);
}

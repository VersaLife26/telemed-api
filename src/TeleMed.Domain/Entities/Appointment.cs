using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Entities;

public class Appointment : Entity, IAuditable
{
    public Guid PatientId { get; init; }
    public Guid DoctorId { get; init; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.PendingPayment;
    public bool IsTest { get; init; }
    public long FeeCents { get; init; }
    public string Currency { get; init; } = PlatformPolicy.Currency;
    public AppointmentIntake Intake { get; init; } = new("", null);

    public required string VisitPatientName { get; init; }
    public DateOnly VisitPatientDateOfBirth { get; init; }
    public Sex? VisitPatientSex { get; init; }
    public decimal? VisitPatientWeightKg { get; init; }
    public string? VisitPatientAllergies { get; init; }

    public DateTimeOffset? PaymentDueAt { get; init; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? NoShowAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public CancellationActor? CancelledBy { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancellationReason { get; set; }
    public int? RefundPercent { get; set; }

    public uint Version { get; init; }
}

public sealed record AppointmentIntake(string Symptoms, string? VisitRelation);

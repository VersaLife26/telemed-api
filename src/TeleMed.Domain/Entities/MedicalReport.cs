using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class MedicalReport : Entity
{
    public const string DefaultAddressee = "To whom it may concern";

    public Guid AppointmentId { get; init; }
    public Guid DoctorId { get; init; }
    public Guid PatientId { get; init; }
    public required string DoctorName { get; init; }
    public required string DoctorSlmc { get; init; }
    public string DoctorQualifications { get; init; } = "";
    public DateTimeOffset IssuedAt { get; init; }
    public string VerificationHmac { get; set; } = "";
    public MedicalReportStatus Status { get; set; } = MedicalReportStatus.Issued;
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public bool IsTest { get; init; }
    public string Addressee { get; init; } = DefaultAddressee;
    public required string ClinicalImpression { get; init; }
    public string? Findings { get; init; }
    public string? Advice { get; init; }
    public FitnessForWork Fitness { get; init; }
    public DateOnly? LeaveFrom { get; init; }
    public DateOnly? LeaveUntil { get; init; }
    public DateOnly? ReturnToWorkOn { get; init; }
    public string? FitnessNotes { get; init; }
    public uint Version { get; init; }
}

using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class Prescription : Entity
{
    public const int MaxItems = 30;
    public const int MaxInvestigations = 20;

    public Guid AppointmentId { get; init; }
    public Guid DoctorId { get; init; }
    public Guid PatientId { get; init; }
    public required string DoctorName { get; init; }
    public required string DoctorSlmc { get; init; }
    public string DoctorQualifications { get; init; } = "";
    public DateTimeOffset IssuedAt { get; init; }
    public string VerificationHmac { get; set; } = "";
    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Issued;
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public bool IsTest { get; init; }
    public List<PrescriptionItem> Items { get; init; } = [];
    public List<string> Investigations { get; init; } = [];
    public uint Version { get; init; }
}

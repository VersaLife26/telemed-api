using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class ClinicalNote : Entity
{
    public const int MaxSectionLength = 20_000;
    public const int MaxDiagnoses = 10;
    public const int MaxAmendmentReasonLength = 1000;

    public Guid AppointmentId { get; init; }
    public Guid DoctorId { get; init; }
    public Guid PatientId { get; init; }
    public string Subjective { get; set; } = "";
    public string Objective { get; set; } = "";
    public string Assessment { get; set; } = "";
    public string Plan { get; set; } = "";
    public ClinicalNoteStatus Status { get; set; } = ClinicalNoteStatus.Draft;
    public DateTimeOffset? FinalisedAt { get; set; }
    public int Revision { get; set; }
    public List<ClinicalNoteDiagnosis> Diagnoses { get; init; } = [];
    public uint Version { get; init; }

    public bool HasContent =>
        !string.IsNullOrWhiteSpace(Subjective)
        || !string.IsNullOrWhiteSpace(Objective)
        || !string.IsNullOrWhiteSpace(Assessment)
        || !string.IsNullOrWhiteSpace(Plan);
}

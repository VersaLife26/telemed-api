using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class DoctorApplication : Entity, IAuditable
{
    public required string Phone { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string DisplayName { get; init; }
    public required string SlmcNumber { get; init; }
    public required string SpecialtyCode { get; init; }
    public List<ConsultationLanguage> Languages { get; init; } = [];
    public string? LanguageOther { get; init; }
    public int ExperienceYears { get; init; }
    public long FeeCents { get; init; }
    public string Bio { get; init; } = "";
    public bool PgimBoardCertified { get; init; }
    public bool IsGeneralPractitioner { get; init; }
    public required string MedicalSchool { get; init; }
    public required string QualificationsText { get; init; }
    public required string AvailabilityNotes { get; init; }
    public List<string> PracticingLocations { get; init; } = [];
    public DateTimeOffset TermsAcceptedAt { get; init; }
    public required string BankName { get; init; }
    public required string BankBranch { get; init; }

    [AuditIgnore]
    public required string BankAccountEncrypted { get; init; }

    [AuditIgnore]
    public string? PasswordHash { get; init; }

    [AuditIgnore]
    public required string UploadTokenHash { get; init; }

    public DoctorApplicationStatus Status { get; set; } = DoctorApplicationStatus.Pending;
    public VerificationChecklist Checklist { get; set; } = new();
    public DateTimeOffset? ReviewStartedAt { get; set; }
    public Guid? ReviewStartedBy { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedBy { get; set; }
    public Guid? DoctorId { get; set; }
    public uint Version { get; init; }

    public bool IsOpen => Status is DoctorApplicationStatus.Pending or DoctorApplicationStatus.UnderReview;
}

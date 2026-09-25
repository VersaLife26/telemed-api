using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Entities;

public class Doctor : Entity, IAuditable
{
    public Guid UserId { get; init; }
    public Guid? ApplicationId { get; init; }
    public required string SlmcNumber { get; init; }
    public required string SpecialtyCode { get; set; }
    public List<string> SubSpecialties { get; set; } = [];
    public required string DisplayName { get; set; }
    public string Bio { get; set; } = "";
    public List<ConsultationLanguage> Languages { get; set; } = [];
    public string? LanguageOther { get; set; }
    public IReadOnlyList<Qualification> Qualifications { get; set; } = [];
    public int ExperienceYears { get; set; }
    public long FeeCents { get; set; }
    public string Currency { get; init; } = PlatformPolicy.Currency;
    public bool AcceptsNewPatients { get; set; } = true;
    public required string BankName { get; set; }
    public required string BankBranch { get; set; }

    [AuditIgnore]
    public required string BankAccountEncrypted { get; set; }

    public string? PhotoStorageKey { get; set; }
    public DoctorStatus Status { get; set; } = DoctorStatus.Active;
    public DateTimeOffset ApprovedAt { get; init; }
    public Guid ApprovedBy { get; init; }
    public DateTimeOffset? SuspendedAt { get; set; }
    public string? SuspendedReason { get; set; }
    public Guid? SuspendedBy { get; set; }
    // Set when the suspension came from suspending the doctor's user account, so reinstating the account undoes only that.
    public bool SuspendedWithUser { get; set; }

    public int SlotDurationMinutes { get; set; } = PlatformPolicy.DefaultSlotDurationMinutes;
    public int BufferMinutes { get; set; } = PlatformPolicy.DefaultBufferMinutes;
    public int MaxPerDay { get; set; } = PlatformPolicy.DefaultMaxPerDay;
    public int AdvanceDays { get; set; } = PlatformPolicy.DefaultAdvanceDays;
    public string TimeZone { get; set; } = PlatformPolicy.TimeZoneId;

    public uint Version { get; init; }
}

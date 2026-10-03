using Microsoft.AspNetCore.Identity;
using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class User : IdentityUser<Guid>, ITimestamped, IAuditable
{
    public User()
    {
        Id = Guid.CreateVersion7();
    }

    public UserRole Role { get; set; }
    public string FullName { get; set; } = "";
    public Language Language { get; set; } = Language.En;
    public DateOnly? DateOfBirth { get; set; }
    public Sex? Sex { get; set; }
    public string? Address { get; set; }
    public string? Allergies { get; set; }
    public string? PhotoStorageKey { get; set; }
    // False for every account that did not confirm Sri Lankan citizenship from a Sri Lankan IP, including accounts created before this existed.
    public bool IsSriLankanCitizen { get; set; }
    public string? RegistrationCountry { get; set; }

    [AuditIgnore]
    public string? NationalIdEncrypted { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset? SuspendedAt { get; set; }
    public string? SuspendedReason { get; set; }
    public Guid? SuspendedBy { get; set; }
    public DateTimeOffset? ErasureDueAt { get; set; }
    public DateTimeOffset? AnonymizedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

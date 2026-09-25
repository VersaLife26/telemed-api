using Microsoft.AspNetCore.Identity;
using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class User : IdentityUser<Guid>, ITimestamped
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
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset? SuspendedAt { get; set; }
    public string? SuspendedReason { get; set; }
    public Guid? SuspendedBy { get; set; }
    public DateTimeOffset? ErasureDueAt { get; set; }
    public DateTimeOffset? AnonymizedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

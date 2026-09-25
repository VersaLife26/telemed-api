using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class AdminUser : Entity, IAuditable
{
    public required string Email { get; init; }
    public required string DisplayName { get; set; }
    public AdminRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    [AuditIgnore]
    public DateTimeOffset? LastLoginAt { get; set; }

    public Guid? CreatedByAdminId { get; init; }
}

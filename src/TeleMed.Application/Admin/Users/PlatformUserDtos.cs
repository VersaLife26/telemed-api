using TeleMed.Application.Admin.Audit;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Users;

public sealed record PlatformUserDto(
    Guid Id,
    UserRole Role,
    string FullName,
    string? Email,
    string? PhoneNumber,
    UserStatus Status,
    Guid? DoctorId,
    DateTimeOffset CreatedAt);

public sealed record PlatformUserDetailDto(
    Guid Id,
    UserRole Role,
    string FullName,
    string? Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    Language Language,
    DateOnly? DateOfBirth,
    Sex? Sex,
    UserStatus Status,
    DateTimeOffset? SuspendedAt,
    string? SuspendedReason,
    Guid? SuspendedBy,
    DateTimeOffset? ErasureDueAt,
    DateTimeOffset? AnonymizedAt,
    Guid? DoctorId,
    DateTimeOffset CreatedAt);

public sealed record AppointmentSummaryDto(int Total, int PendingPayment, int Confirmed, int Completed, int NoShow, int Cancelled, DateTimeOffset? LastStartAt);

public sealed record UserActivityDto(AppointmentSummaryDto Appointments, IReadOnlyList<AuditEntryDto> Audit);

public sealed record PlatformUserQuery : PageQuery
{
    public string? Q { get; init; }
    public UserRole? Role { get; init; }
    public UserStatus? Status { get; init; }
}

public sealed record SuspendUserRequest(string Reason);

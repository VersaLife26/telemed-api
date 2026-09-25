using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Users;

public interface IPlatformUserRepository
{
    Task<(IReadOnlyList<PlatformUserDto> Items, long Total)> ListAsync(string? search, UserRole? role, UserStatus? status, int skip, int take, CancellationToken ct);
    Task<PlatformUserDetailDto?> FindAsync(Guid id, CancellationToken ct);
    // Non-test appointments where the user is the patient or the doctor.
    Task<AppointmentSummaryDto> SummarizeAppointmentsAsync(Guid userId, CancellationToken ct);
}

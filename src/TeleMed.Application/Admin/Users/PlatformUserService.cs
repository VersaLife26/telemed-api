using TeleMed.Application.Abstractions;
using TeleMed.Application.Admin.Audit;
using TeleMed.Application.Appointments;
using TeleMed.Application.Auth;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Application.Payments;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Users;

// Suspension revokes every refresh token and rotates the security stamp, so live access tokens stop working at once
// and sign-in, refresh and every authenticated call (booking included) are refused until reinstated.
// A suspended patient's upcoming bookings are cancelled with a full refund; a suspended doctor's profile is hidden
// (existing appointments stay), and reinstating the user lifts only a doctor suspension that this suspension caused.
public sealed class PlatformUserService(
    ICurrentActor actor,
    IPlatformUserRepository users,
    IUserAccounts accounts,
    IAuditLogRepository audit,
    IAppointmentRepository appointments,
    IDoctorRepository doctors,
    ICalendarLock calendar,
    PaymentLifecycle lifecycle,
    SessionIssuer sessions,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public const int ActivityAuditLimit = 100;

    public async Task<PagedResult<PlatformUserDto>> ListAsync(PlatformUserQuery query, CancellationToken ct)
    {
        var search = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
        var (items, total) = await users.ListAsync(search, query.Role, query.Status, query.Skip, query.PageSize, ct);
        return new PagedResult<PlatformUserDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<PlatformUserDetailDto> GetAsync(Guid id, CancellationToken ct) => await users.FindAsync(id, ct) ?? throw NotFound();

    public async Task<UserActivityDto> GetActivityAsync(Guid id, CancellationToken ct)
    {
        _ = await users.FindAsync(id, ct) ?? throw NotFound();
        return new UserActivityDto(
            await users.SummarizeAppointmentsAsync(id, ct),
            (await audit.ListByActorAsync(id, ActivityAuditLimit, ct)).Select(a => a.ToDto()).ToList());
    }

    public async Task<PlatformUserDetailDto> SuspendAsync(Guid id, SuspendUserRequest request, CancellationToken ct)
    {
        var admin = actor.RequireAdmin();
        var user = await LoadAsync(id, ct);
        if (user.Status != UserStatus.Active)
        {
            throw new ConflictException("user_not_active", "Only an active user can be suspended.");
        }

        var now = time.GetUtcNow();
        var reason = request.Reason.Trim();
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        user.Status = UserStatus.Suspended;
        user.SuspendedAt = now;
        user.SuspendedReason = reason;
        user.SuspendedBy = admin.Id;
        await accounts.UpdateAsync(user);
        await sessions.RevokeAllAsync(user, ct);

        if (user.Role == UserRole.Patient)
        {
            foreach (var doctorId in (await appointments.ListLiveDoctorIdsForPatientAsync(user.Id, now, ct)).Order())
            {
                await calendar.LockDoctorAsync(doctorId, ct);
            }

            foreach (var appointment in (await appointments.ListLiveForPatientForUpdateAsync(user.Id, now, ct)).OrderBy(a => a.Id))
            {
                await lifecycle.CancelForPatientSuspensionAsync(appointment, await lifecycle.LockPaymentAsync(appointment.Id, ct), admin.Id, ct);
            }
        }
        else if (await doctors.FindByUserIdAsync(user.Id, ct) is { Status: DoctorStatus.Active } doctor)
        {
            doctor.Status = DoctorStatus.Suspended;
            doctor.SuspendedAt = now;
            doctor.SuspendedReason = reason;
            doctor.SuspendedBy = admin.Id;
            doctor.SuspendedWithUser = true;
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PlatformUserDetailDto> ReinstateAsync(Guid id, CancellationToken ct)
    {
        actor.RequireAdmin();
        var user = await LoadAsync(id, ct);
        if (user.Status != UserStatus.Suspended)
        {
            throw new ConflictException("user_not_suspended", "Only a suspended user can be reinstated.");
        }

        user.Status = UserStatus.Active;
        user.SuspendedAt = null;
        user.SuspendedReason = null;
        user.SuspendedBy = null;
        await accounts.UpdateAsync(user);
        if (await doctors.FindByUserIdAsync(user.Id, ct) is { Status: DoctorStatus.Suspended, SuspendedWithUser: true } doctor)
        {
            doctor.Status = DoctorStatus.Active;
            doctor.SuspendedAt = null;
            doctor.SuspendedReason = null;
            doctor.SuspendedBy = null;
            doctor.SuspendedWithUser = false;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task ResetPasswordAsync(Guid id, ResetUserPasswordRequest request, CancellationToken ct)
    {
        actor.RequireAdmin();
        var user = await LoadAsync(id, ct);
        if (user.Status == UserStatus.Deleted)
        {
            throw new ConflictException("user_deleted", "Cannot reset password for a deleted user.");
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await accounts.ResetPasswordAsync(user, request.NewPassword);
        await sessions.RevokeAllAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<User> LoadAsync(Guid id, CancellationToken ct) => await accounts.FindByIdAsync(id, ct) ?? throw NotFound();

    private static NotFoundException NotFound() => new("User not found.");
}

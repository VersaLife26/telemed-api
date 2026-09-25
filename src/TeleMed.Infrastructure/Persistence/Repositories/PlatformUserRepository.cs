using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Admin.Users;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class PlatformUserRepository(AppDbContext db) : IPlatformUserRepository
{
    public async Task<(IReadOnlyList<PlatformUserDto> Items, long Total)> ListAsync(
        string? search, UserRole? role, UserStatus? status, int skip, int take, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (role is { } r)
        {
            query = query.Where(u => u.Role == r);
        }

        if (status is { } s)
        {
            query = query.Where(u => u.Status == s);
        }

        if (search is not null)
        {
            var lowered = search.ToLowerInvariant();
            query = query.Where(u => u.FullName.ToLower().Contains(lowered)
                || (u.Email != null && u.Email.ToLower().Contains(lowered))
                || (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
        }

        var total = await query.LongCountAsync(ct);
        var items = await (
                from u in query.OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id).Skip(skip).Take(take)
                join d in db.Doctors on u.Id equals d.UserId into doctors
                from d in doctors.DefaultIfEmpty()
                orderby u.CreatedAt descending, u.Id
                select new PlatformUserDto(u.Id, u.Role, u.FullName, u.Email, u.PhoneNumber, u.Status, d == null ? null : d.Id, u.CreatedAt))
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<PlatformUserDetailDto?> FindAsync(Guid id, CancellationToken ct) =>
        (from u in db.Users.AsNoTracking()
         where u.Id == id
         join d in db.Doctors on u.Id equals d.UserId into doctors
         from d in doctors.DefaultIfEmpty()
         select new PlatformUserDetailDto(
             u.Id, u.Role, u.FullName, u.Email, u.EmailConfirmed, u.PhoneNumber, u.Language, u.DateOfBirth, u.Sex, u.Status,
             u.SuspendedAt, u.SuspendedReason, u.SuspendedBy, u.ErasureDueAt, u.AnonymizedAt, d == null ? null : d.Id, u.CreatedAt))
        .SingleOrDefaultAsync(ct);

    public async Task<AppointmentSummaryDto> SummarizeAppointmentsAsync(Guid userId, CancellationToken ct)
    {
        var doctorId = await db.Doctors.Where(d => d.UserId == userId).Select(d => (Guid?)d.Id).SingleOrDefaultAsync(ct);
        var counts = await db.Appointments.AsNoTracking()
            .Where(a => !a.IsTest && (a.PatientId == userId || a.DoctorId == doctorId))
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), LastStartAt = g.Max(a => (DateTimeOffset?)a.StartAt) })
            .ToListAsync(ct);
        int Count(AppointmentStatus status) => counts.SingleOrDefault(c => c.Status == status)?.Count ?? 0;
        return new AppointmentSummaryDto(
            counts.Sum(c => c.Count),
            Count(AppointmentStatus.PendingPayment),
            Count(AppointmentStatus.Confirmed),
            Count(AppointmentStatus.Completed),
            Count(AppointmentStatus.NoShow),
            Count(AppointmentStatus.Cancelled),
            counts.Max(c => c.LastStartAt));
    }
}

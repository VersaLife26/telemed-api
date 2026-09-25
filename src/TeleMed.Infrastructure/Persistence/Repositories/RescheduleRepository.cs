using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Reschedules;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class RescheduleRepository(AppDbContext db) : IRescheduleRepository
{
    public void Add(RescheduleRequest request) => db.RescheduleRequests.Add(request);

    public Task<RescheduleRequest?> FindAsync(Guid id, CancellationToken ct) =>
        db.RescheduleRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);

    public Task<RescheduleRequest?> FindForUpdateAsync(Guid id, CancellationToken ct) =>
        db.RescheduleRequests.SingleOrDefaultAsync(r => r.Id == id, ct);

    public Task<RescheduleRequest?> FindPendingForAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        db.RescheduleRequests.SingleOrDefaultAsync(r => r.AppointmentId == appointmentId && r.Status == RescheduleStatus.Pending, ct);

    public async Task<IReadOnlyList<RescheduleRequest>> ListPendingForUpdateAsync(
        IReadOnlyCollection<Guid> doctorIds, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        await db.RescheduleRequests
            .Where(r => doctorIds.Contains(r.DoctorId) && r.Status == RescheduleStatus.Pending && r.ProposedStartAt < to && r.ProposedEndAt > from)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RescheduleRequest>> ListForAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        await db.RescheduleRequests.AsNoTracking()
            .Where(r => r.AppointmentId == appointmentId)
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<RescheduleRequest> Items, long Total)> ListAsync(RescheduleStatus? status, int skip, int take, CancellationToken ct)
    {
        var query = db.RescheduleRequests.AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(r => r.Status == s);
        }

        var total = await query.LongCountAsync(ct);
        var items = await query.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<ExpiredReschedule>> ListExpiredPendingAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        await db.RescheduleRequests.AsNoTracking()
            .Where(r => r.Status == RescheduleStatus.Pending && r.OriginalStartAt <= now)
            .OrderBy(r => r.OriginalStartAt)
            .Take(limit)
            .Select(r => new ExpiredReschedule(r.Id, r.DoctorId))
            .ToListAsync(ct);
}

using Microsoft.EntityFrameworkCore;
using TeleMed.Application.DoctorApplications;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class DoctorApplicationRepository(AppDbContext db) : IDoctorApplicationRepository
{
    public void Add(DoctorApplication application) => db.DoctorApplications.Add(application);

    public Task<DoctorApplication?> FindAsync(Guid id, CancellationToken ct) =>
        db.DoctorApplications.SingleOrDefaultAsync(a => a.Id == id, ct);

    public Task<DoctorApplication?> FindLatestByPhoneAsync(string phone, CancellationToken ct) =>
        db.DoctorApplications.AsNoTracking()
            .Where(a => a.Phone == phone)
            .OrderByDescending(a => a.Status == DoctorApplicationStatus.Pending || a.Status == DoctorApplicationStatus.UnderReview)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<DoctorApplication> Items, long Total)> ListAsync(
        DoctorApplicationStatus? status, int skip, int take, CancellationToken ct)
    {
        var query = db.DoctorApplications.AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(a => a.Status == s);
        }

        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }
}

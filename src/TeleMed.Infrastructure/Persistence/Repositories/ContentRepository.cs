using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Admin.Content;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class ContentRepository(AppDbContext db) : IContentRepository
{
    public async Task<IReadOnlyList<Specialty>> ListSpecialtiesAsync(CancellationToken ct) =>
        await db.Specialties.AsNoTracking().OrderBy(s => s.DisplayOrder).ThenBy(s => s.Code).ToListAsync(ct);

    public Task<Specialty?> FindSpecialtyAsync(string code, CancellationToken ct) =>
        db.Specialties.SingleOrDefaultAsync(s => s.Code == code, ct);

    public void AddSpecialty(Specialty specialty) => db.Specialties.Add(specialty);

    public async Task<(IReadOnlyList<Drug> Items, long Total)> ListDrugsAsync(string? search, int skip, int take, CancellationToken ct)
    {
        var query = db.Drugs.AsNoTracking();
        if (search is not null)
        {
            var lowered = search.ToLowerInvariant();
            query = query.Where(d => d.Name.ToLower().Contains(lowered) || d.GenericName.ToLower().Contains(lowered));
        }

        var total = await query.LongCountAsync(ct);
        var items = await query.OrderBy(d => d.Name).ThenBy(d => d.Strength).ThenBy(d => d.Id).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public Task<Drug?> FindDrugAsync(Guid id, CancellationToken ct) => db.Drugs.SingleOrDefaultAsync(d => d.Id == id, ct);

    public void AddDrug(Drug drug) => db.Drugs.Add(drug);

    public async Task<IReadOnlyList<WaitingRoomItem>> ListWaitingRoomItemsAsync(bool activeOnly, CancellationToken ct)
    {
        var query = db.WaitingRoomItems.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(i => i.IsActive);
        }

        return await query.OrderBy(i => i.DisplayOrder).ThenBy(i => i.CreatedAt).ToListAsync(ct);
    }

    public Task<WaitingRoomItem?> FindWaitingRoomItemAsync(Guid id, CancellationToken ct) =>
        db.WaitingRoomItems.SingleOrDefaultAsync(i => i.Id == id, ct);

    public void AddWaitingRoomItem(WaitingRoomItem item) => db.WaitingRoomItems.Add(item);
}

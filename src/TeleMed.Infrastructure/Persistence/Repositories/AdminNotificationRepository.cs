using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Admin.Notifications;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class AdminNotificationRepository(AppDbContext db) : IAdminNotificationRepository
{
    public void Add(AdminNotification notification) => db.AdminNotifications.Add(notification);

    public async Task<(IReadOnlyList<AdminNotification> Items, long Total)> ListAsync(bool unreadOnly, int skip, int take, CancellationToken ct)
    {
        var query = db.AdminNotifications.AsNoTracking();
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public Task<long> CountUnreadAsync(CancellationToken ct) => db.AdminNotifications.LongCountAsync(n => n.ReadAt == null, ct);

    public Task<AdminNotification?> FindForUpdateAsync(Guid id, CancellationToken ct) => db.AdminNotifications.SingleOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<AdminNotification>> ListUnreadForUpdateAsync(CancellationToken ct) =>
        await db.AdminNotifications.Where(n => n.ReadAt == null).ToListAsync(ct);
}

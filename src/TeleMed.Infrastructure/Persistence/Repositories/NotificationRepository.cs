using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Notifications;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRepository(AppDbContext db) : INotificationRepository
{
    public void Add(Notification notification) => db.Notifications.Add(notification);

    public async Task<IReadOnlySet<string>> ExistingDedupeKeysAsync(IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        var existing = await db.Notifications.Where(n => keys.Contains(n.DedupeKey)).Select(n => n.DedupeKey).ToListAsync(ct);
        existing.AddRange(db.Notifications.Local.Where(n => keys.Contains(n.DedupeKey)).Select(n => n.DedupeKey));
        return existing.ToHashSet();
    }

    public async Task<IReadOnlyList<Notification>> ClaimDueAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        await db.Notifications
            .FromSql($"""
                SELECT * FROM notifications
                WHERE status IN ('pending', 'sending') AND next_attempt_at <= {now}
                ORDER BY next_attempt_at, id
                LIMIT {limit}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Notification>> ListSentWithBodyBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken ct) =>
        await db.Notifications
            .Where(n => n.Status == NotificationStatus.Sent && n.SentAt < cutoff && n.Body != null)
            .OrderBy(n => n.Id)
            .Take(limit)
            .ToListAsync(ct);
}

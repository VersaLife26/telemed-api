using TeleMed.Domain.Entities;

namespace TeleMed.Application.Admin.Notifications;

public interface IAdminNotificationRepository
{
    void Add(AdminNotification notification);
    Task<(IReadOnlyList<AdminNotification> Items, long Total)> ListAsync(bool unreadOnly, int skip, int take, CancellationToken ct);
    Task<long> CountUnreadAsync(CancellationToken ct);
    Task<AdminNotification?> FindForUpdateAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AdminNotification>> ListUnreadForUpdateAsync(CancellationToken ct);
}

using TeleMed.Application.Abstractions;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Notifications;

// One inbox shared by every admin: reading an item marks it read for all of them.
public sealed class AdminNotificationService(IAdminNotificationRepository notifications, IUnitOfWork unitOfWork, TimeProvider time)
{
    // Stages the item; the caller's unit of work commits it.
    public void Add(AdminNotificationKind kind, string title, string body, string? href, Guid? resourceId) =>
        notifications.Add(new AdminNotification { Kind = kind, Title = title, Body = body, Href = href, ResourceId = resourceId });

    public async Task<PagedResult<AdminNotificationDto>> ListAsync(AdminNotificationQuery query, CancellationToken ct)
    {
        var (items, total) = await notifications.ListAsync(query.UnreadOnly, query.Skip, query.PageSize, ct);
        return new PagedResult<AdminNotificationDto>(items.Select(n => n.ToDto()).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<UnreadCountDto> CountUnreadAsync(CancellationToken ct) => new(await notifications.CountUnreadAsync(ct));

    public async Task<AdminNotificationDto> MarkReadAsync(Guid id, CancellationToken ct)
    {
        var notification = await notifications.FindForUpdateAsync(id, ct) ?? throw new NotFoundException("Notification not found.");
        notification.ReadAt ??= time.GetUtcNow();
        await unitOfWork.SaveChangesAsync(ct);
        return notification.ToDto();
    }

    public async Task MarkAllReadAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var notification in await notifications.ListUnreadForUpdateAsync(ct))
        {
            notification.ReadAt = now;
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}

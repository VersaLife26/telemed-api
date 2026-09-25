using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Admin.Notifications;
using TeleMed.Application.Common;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin/notifications")]
[AdminAuthorize]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminNotificationsController(AdminNotificationService notifications) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AdminNotificationDto>> List([FromQuery] AdminNotificationQuery query, CancellationToken ct) => notifications.ListAsync(query, ct);

    [HttpGet("unread-count")]
    public Task<UnreadCountDto> UnreadCount(CancellationToken ct) => notifications.CountUnreadAsync(ct);

    [HttpPost("{id:guid}/read")]
    public Task<AdminNotificationDto> MarkRead(Guid id, CancellationToken ct) => notifications.MarkReadAsync(id, ct);

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(ct);
        return NoContent();
    }
}

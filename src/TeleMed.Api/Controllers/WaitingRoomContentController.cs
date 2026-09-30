using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Application.Admin.Content;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("waiting-room-content")]
[Authorize(Roles = UserRoleNames.Patient)]
public sealed class WaitingRoomContentController(ContentService content) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<WaitingRoomItemDto>> List(CancellationToken ct) =>
        content.ListPublishedWaitingRoomItemsAsync(ct);
}

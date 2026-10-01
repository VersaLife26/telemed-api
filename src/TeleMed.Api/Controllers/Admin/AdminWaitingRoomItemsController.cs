using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Admin.Content;
using TeleMed.Application.Permissions;
using TeleMed.Domain.Rules;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin/waiting-room-items")]
[AdminAuthorize(AdminPermission.Content)]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminWaitingRoomItemsController(ContentService content) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AdminWaitingRoomItemDto>> List(CancellationToken ct) => content.ListWaitingRoomItemsAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<AdminWaitingRoomItemDto> Get(Guid id, CancellationToken ct) => content.GetWaitingRoomItemAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<AdminWaitingRoomItemDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> Create(SaveWaitingRoomItemRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await content.CreateWaitingRoomItemAsync(request, ct));

    [HttpPut("{id:guid}")]
    public Task<AdminWaitingRoomItemDto> Update(Guid id, SaveWaitingRoomItemRequest request, CancellationToken ct) =>
        content.UpdateWaitingRoomItemAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> Delete(Guid id, CancellationToken ct)
    {
        await content.DeleteWaitingRoomItemAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PlatformPolicy.ProfilePhotoMaxBytes + (64 * 1024))]
    public async Task<AdminWaitingRoomItemDto> PutImage(Guid id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await content.SetWaitingRoomItemImageAsync(id, stream, ct);
    }

    [HttpDelete("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> DeleteImage(Guid id, CancellationToken ct)
    {
        await content.DeleteWaitingRoomItemImageAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/video")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PlatformPolicy.WaitingRoomVideoMaxBytes + (64 * 1024))]
    [RequestFormLimits(MultipartBodyLengthLimit = PlatformPolicy.WaitingRoomVideoMaxBytes + (64 * 1024))]
    public async Task<AdminWaitingRoomItemDto> PutVideo(Guid id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await content.SetWaitingRoomItemVideoAsync(id, stream, ct);
    }

    [HttpDelete("{id:guid}/video")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> DeleteVideo(Guid id, CancellationToken ct)
    {
        await content.DeleteWaitingRoomItemVideoAsync(id, ct);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Testing;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("test/captured-messages")]
[AllowAnonymous]
[TestEndpoint(TestFeature.CaptureInbox)]
public sealed class TestCapturedMessagesController(CapturedMessagesService messages) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CapturedMessageDto>> List([FromQuery] CapturedMessageQuery query, CancellationToken ct) =>
        messages.ListAsync(query, ct);

    [HttpGet("latest")]
    public Task<CapturedMessageDto> Latest([FromQuery] CapturedMessageQuery query, CancellationToken ct) =>
        messages.LatestAsync(query, ct);

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> Clear(CancellationToken ct)
    {
        await messages.ClearAsync(ct);
        return NoContent();
    }
}

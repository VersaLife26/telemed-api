using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Testing;

namespace TeleMed.Api.Controllers;

// Anonymous at the routing layer so a switched-off endpoint is a 404 for everyone; the service requires a signed-in caller.
[ApiController]
[Route("test/instant-meetings")]
[AllowAnonymous]
[TestEndpoint(TestFeature.InstantMeetings)]
public sealed class TestInstantMeetingsController(InstantMeetingService meetings) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<InstantMeetingDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> Create(CreateInstantMeetingRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await meetings.CreateAsync(request, ct));
}

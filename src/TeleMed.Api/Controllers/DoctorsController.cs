using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeleMed.Api.RateLimiting;
using TeleMed.Application.Common;
using TeleMed.Application.Doctors;
using TeleMed.Application.Scheduling;
using TeleMed.Application.Users;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("doctors")]
[AllowAnonymous]
public sealed class DoctorsController(DoctorDirectoryService directory, SlotService slots) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PublicDoctorDto>> Search([FromQuery] DoctorSearchQuery query, CancellationToken ct) =>
        directory.SearchAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<PublicDoctorDto> Get(Guid id, CancellationToken ct) => directory.GetAsync(id, ct);

    [HttpGet("{id:guid}/photo")]
    public Task<PhotoUrlDto> GetPhoto(Guid id, CancellationToken ct) => directory.GetPhotoUrlAsync(id, ct);

    [HttpGet("{id:guid}/slots")]
    [EnableRateLimiting(RateLimitingSetup.Slots)]
    public Task<SlotsDto> GetSlots(Guid id, [FromQuery] SlotQuery query, CancellationToken ct) => slots.GetAsync(id, query, ct);
}

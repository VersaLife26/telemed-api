using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Admin.Content;
using TeleMed.Application.Permissions;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin/specialties")]
[AdminAuthorize(AdminPermission.Content)]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminSpecialtiesController(ContentService content) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AdminSpecialtyDto>> List(CancellationToken ct) => content.ListSpecialtiesAsync(ct);

    [HttpGet("{code}")]
    public Task<AdminSpecialtyDto> Get(string code, CancellationToken ct) => content.GetSpecialtyAsync(code, ct);

    [HttpPost]
    [ProducesResponseType<AdminSpecialtyDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> Create(CreateSpecialtyRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await content.CreateSpecialtyAsync(request, ct));

    [HttpPut("{code}")]
    public Task<AdminSpecialtyDto> Update(string code, UpdateSpecialtyRequest request, CancellationToken ct) =>
        content.UpdateSpecialtyAsync(code, request, ct);

    [HttpDelete("{code}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> Delete(string code, CancellationToken ct)
    {
        await content.DeleteSpecialtyAsync(code, ct);
        return NoContent();
    }
}

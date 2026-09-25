using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Admin.Content;
using TeleMed.Application.Common;
using TeleMed.Application.Permissions;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin/drugs")]
[AdminAuthorize(AdminPermission.Content)]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminDrugsController(ContentService content) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AdminDrugDto>> List([FromQuery] AdminDrugQuery query, CancellationToken ct) => content.ListDrugsAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<AdminDrugDto> Get(Guid id, CancellationToken ct) => content.GetDrugAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<AdminDrugDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> Create(SaveDrugRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await content.CreateDrugAsync(request, ct));

    [HttpPut("{id:guid}")]
    public Task<AdminDrugDto> Update(Guid id, SaveDrugRequest request, CancellationToken ct) => content.UpdateDrugAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> Delete(Guid id, CancellationToken ct)
    {
        await content.DeactivateDrugAsync(id, ct);
        return NoContent();
    }
}

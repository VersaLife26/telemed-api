using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TeleMed.Api.Auth;
using TeleMed.Application.Admin.Disputes;
using TeleMed.Application.Common;
using TeleMed.Application.Permissions;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin/disputes")]
[AdminAuthorize(AdminPermission.Disputes)]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminDisputesController(DisputeService disputes) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<DisputeDto>> List([FromQuery] DisputeQuery query, CancellationToken ct) => disputes.ListAsync(query, ct);

    [HttpPost]
    [ProducesResponseType<DisputeDetailDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> Create(CreateDisputeRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await disputes.CreateAsync(request, ct));

    [HttpGet("{id:guid}")]
    public Task<DisputeDetailDto> Get(Guid id, CancellationToken ct) => disputes.GetAsync(id, ct);

    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType<DisputeCommentDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> AddComment(Guid id, AddDisputeCommentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await disputes.AddCommentAsync(id, request, ct));

    [HttpPost("{id:guid}/assign")]
    public Task<DisputeDetailDto> Assign(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AssignDisputeRequest? request, CancellationToken ct) =>
        disputes.AssignAsync(id, request ?? new AssignDisputeRequest(null), ct);

    [HttpPost("{id:guid}/resolve")]
    public Task<DisputeDetailDto> Resolve(Guid id, ResolveDisputeRequest request, CancellationToken ct) => disputes.ResolveAsync(id, request, ct);

    [HttpPost("{id:guid}/close")]
    public Task<DisputeDetailDto> Close(Guid id, CancellationToken ct) => disputes.CloseAsync(id, ct);
}

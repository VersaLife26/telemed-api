using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Application.Common;
using TeleMed.Application.CustomerCare;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{UserRoleNames.Patient},{UserRoleNames.Doctor}")]
public sealed class CustomerCareController(CustomerCareService care) : ControllerBase
{
    [HttpGet("customer-care")]
    public Task<PagedResult<CustomerCareSummaryDto>> List([FromQuery] CustomerCareQuery query, CancellationToken ct) =>
        care.ListAsync(query, ct);

    [HttpPost("customer-care")]
    [ProducesResponseType<CustomerCareThreadDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> Open(OpenCustomerCareRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await care.OpenAsync(request, ct));

    [HttpGet("customer-care/{id:guid}")]
    public Task<CustomerCareThreadDto> Get(Guid id, CancellationToken ct) => care.GetAsync(id, ct);

    [HttpPost("customer-care/{id:guid}/messages")]
    [ProducesResponseType<CustomerCareMessageDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> Reply(Guid id, CustomerCareMessageRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await care.ReplyAsync(id, request, ct));
}

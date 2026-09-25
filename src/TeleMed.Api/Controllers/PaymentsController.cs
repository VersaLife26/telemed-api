using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Application.Common;
using TeleMed.Application.Payments;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("payments")]
[Authorize(Roles = UserRoleNames.Patient)]
public sealed class PaymentsController(PaymentService payments) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PaymentDto>> List([FromQuery] PaymentQuery query, CancellationToken ct) => payments.ListMineAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<PaymentDto> Get(Guid id, CancellationToken ct) => payments.GetMineAsync(id, ct);

    [HttpPost("{id:guid}/mock/complete")]
    public Task<PaymentDto> CompleteMock(Guid id, MockCompleteRequest request, CancellationToken ct) => payments.CompleteMockAsync(id, request, ct);
}

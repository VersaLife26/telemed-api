using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Application.Reschedules;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("reschedule-requests")]
[Authorize(Roles = UserRoleNames.Patient)]
public sealed class RescheduleRequestsController(RescheduleService reschedules) : ControllerBase
{
    [HttpPost("{id:guid}/accept")]
    public Task<RescheduleDecisionDto> Accept(Guid id, CancellationToken ct) => reschedules.AcceptAsync(id, ct);

    [HttpPost("{id:guid}/decline")]
    public Task<RescheduleDecisionDto> Decline(Guid id, CancellationToken ct) => reschedules.DeclineAsync(id, ct);
}

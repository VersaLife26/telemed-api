using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Admin.Appointments;
using TeleMed.Application.Admin.Audit;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Permissions;
using TeleMed.Application.Reschedules;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin")]
[AdminAuthorize(AdminPermission.Appointments)]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminAppointmentsController(AdminAppointmentService appointments, RescheduleService reschedules) : ControllerBase
{
    [HttpGet("appointments")]
    public Task<PagedResult<AppointmentDto>> List([FromQuery] AdminAppointmentQuery query, CancellationToken ct) => appointments.ListAsync(query, ct);

    [HttpGet("appointments/{id:guid}")]
    public Task<AdminAppointmentDetailDto> Get(Guid id, CancellationToken ct) => appointments.GetAsync(id, ct);

    [HttpGet("appointments/{id:guid}/audit")]
    public Task<IReadOnlyList<AuditEntryDto>> Audit(Guid id, CancellationToken ct) => appointments.ListAuditAsync(id, ct);

    [HttpPost("appointments/{id:guid}/cancel")]
    public Task<AppointmentDto> Cancel(Guid id, AdminCancelAppointmentRequest request, CancellationToken ct) => appointments.CancelAsync(id, request, ct);

    [HttpGet("reschedule-requests")]
    public Task<PagedResult<RescheduleRequestDto>> ListRescheduleRequests([FromQuery] RescheduleQuery query, CancellationToken ct) =>
        reschedules.ListAsync(query, ct);

    [HttpPost("reschedule-requests/{id:guid}/accept")]
    public Task<RescheduleDecisionDto> AcceptRescheduleRequest(Guid id, CancellationToken ct) => reschedules.AdminAcceptAsync(id, ct);

    [HttpPost("reschedule-requests/{id:guid}/decline")]
    public Task<RescheduleDecisionDto> DeclineRescheduleRequest(Guid id, CancellationToken ct) => reschedules.AdminDeclineAsync(id, ct);
}

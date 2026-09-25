using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Payments;
using TeleMed.Application.Reschedules;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("appointments")]
[Authorize(Roles = $"{UserRoleNames.Patient},{UserRoleNames.Doctor}")]
public sealed class AppointmentsController(AppointmentService appointments, PaymentService payments, RescheduleService reschedules) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = UserRoleNames.Patient)]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> Book(BookAppointmentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await appointments.BookAsync(request, ct));

    [HttpGet]
    public Task<PagedResult<AppointmentDto>> List([FromQuery] AppointmentQuery query, CancellationToken ct) => appointments.ListAsync(query, ct);

    [HttpGet("last-visit-details")]
    [Authorize(Roles = UserRoleNames.Patient)]
    public Task<LastVisitDetailsDto> LastVisitDetails(CancellationToken ct) => appointments.GetLastVisitDetailsAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<AppointmentDto> Get(Guid id, CancellationToken ct) => appointments.GetAsync(id, ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<AppointmentDto> Cancel(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelAppointmentRequest? request,
        CancellationToken ct) =>
        appointments.CancelAsync(id, request, ct);

    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<AppointmentDto> Complete(Guid id, CancellationToken ct) => appointments.CompleteAsync(id, ct);

    [HttpPost("{id:guid}/no-show")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<AppointmentDto> NoShow(Guid id, CancellationToken ct) => appointments.MarkNoShowAsync(id, ct);

    [HttpPost("{id:guid}/reschedule-requests")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    [ProducesResponseType<RescheduleRequestDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> ProposeReschedule(Guid id, ProposeRescheduleRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await reschedules.ProposeAsync(id, request, ct));

    [HttpGet("{id:guid}/reschedule-requests")]
    public Task<IReadOnlyList<RescheduleRequestDto>> ListRescheduleRequests(Guid id, CancellationToken ct) =>
        reschedules.ListForAppointmentAsync(id, ct);

    [HttpGet("{id:guid}/payment")]
    [Authorize(Roles = UserRoleNames.Patient)]
    public Task<OrderSummaryDto> GetPayment(Guid id, CancellationToken ct) => payments.GetSummaryAsync(id, ct);

    [HttpPut("{id:guid}/payment/promo")]
    [Authorize(Roles = UserRoleNames.Patient)]
    public Task<OrderSummaryDto> ApplyPromo(Guid id, ApplyPromoRequest request, CancellationToken ct) => payments.ApplyPromoAsync(id, request, ct);

    [HttpDelete("{id:guid}/payment/promo")]
    [Authorize(Roles = UserRoleNames.Patient)]
    public Task<OrderSummaryDto> RemovePromo(Guid id, CancellationToken ct) => payments.RemovePromoAsync(id, ct);

    [HttpPost("{id:guid}/payment/intent")]
    [Authorize(Roles = UserRoleNames.Patient)]
    public Task<PaymentIntentDto> CreateIntent(Guid id, CreateIntentRequest request, CancellationToken ct) => payments.CreateIntentAsync(id, request, ct);
}

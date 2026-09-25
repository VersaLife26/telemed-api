using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using TeleMed.Api.RateLimiting;
using TeleMed.Application.Common;
using TeleMed.Application.Prescriptions;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{UserRoleNames.Patient},{UserRoleNames.Doctor}")]
public sealed class PrescriptionsController(PrescriptionService prescriptions) : ControllerBase
{
    [HttpPost("appointments/{appointmentId:guid}/prescription")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    [ProducesResponseType<PrescriptionDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> Issue(Guid appointmentId, IssuePrescriptionRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await prescriptions.IssueAsync(appointmentId, request, ct));

    [HttpGet("appointments/{appointmentId:guid}/prescription")]
    public Task<PrescriptionDto> GetForAppointment(Guid appointmentId, CancellationToken ct) =>
        prescriptions.GetForAppointmentAsync(appointmentId, ct);

    [HttpGet("prescriptions")]
    public Task<PagedResult<PrescriptionDto>> List([FromQuery] PrescriptionQuery query, CancellationToken ct) => prescriptions.ListMineAsync(query, ct);

    [HttpGet("prescriptions/{id:guid}")]
    public Task<PrescriptionDto> Get(Guid id, CancellationToken ct) => prescriptions.GetAsync(id, ct);

    [HttpGet("prescriptions/{id:guid}/pdf")]
    [Produces("application/pdf")]
    public async Task<FileContentResult> Pdf(Guid id, CancellationToken ct)
    {
        var pdf = await prescriptions.RenderPdfAsync(id, ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    [HttpPost("prescriptions/{id:guid}/cancel")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<PrescriptionDto> Cancel(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelPrescriptionRequest? request,
        CancellationToken ct) =>
        prescriptions.CancelAsync(id, request, ct);

    [HttpGet("prescriptions/{id:guid}/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.PrescriptionVerify)]
    public Task<PrescriptionVerificationDto> Verify(Guid id, [FromQuery] string? h, CancellationToken ct) => prescriptions.VerifyAsync(id, h, ct);
}

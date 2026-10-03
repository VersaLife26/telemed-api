using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using TeleMed.Api.RateLimiting;
using TeleMed.Application.Common;
using TeleMed.Application.MedicalReports;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{UserRoleNames.Patient},{UserRoleNames.Doctor}")]
public sealed class MedicalReportsController(MedicalReportService reports) : ControllerBase
{
    [HttpPost("appointments/{appointmentId:guid}/medical-report")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    [ProducesResponseType<MedicalReportDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> Issue(Guid appointmentId, IssueMedicalReportRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await reports.IssueAsync(appointmentId, request, ct));

    [HttpGet("appointments/{appointmentId:guid}/medical-report")]
    public Task<MedicalReportDto> GetForAppointment(Guid appointmentId, CancellationToken ct) =>
        reports.GetForAppointmentAsync(appointmentId, ct);

    [HttpGet("medical-reports")]
    public Task<PagedResult<MedicalReportDto>> List([FromQuery] MedicalReportQuery query, CancellationToken ct) =>
        reports.ListMineAsync(query, ct);

    [HttpGet("medical-reports/{id:guid}")]
    public Task<MedicalReportDto> Get(Guid id, CancellationToken ct) => reports.GetAsync(id, ct);

    [HttpGet("medical-reports/{id:guid}/pdf")]
    [Produces("application/pdf")]
    public async Task<FileContentResult> Pdf(Guid id, CancellationToken ct)
    {
        var pdf = await reports.RenderPdfAsync(id, ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    [HttpPost("medical-reports/{id:guid}/cancel")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<MedicalReportDto> Cancel(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelMedicalReportRequest? request,
        CancellationToken ct) =>
        reports.CancelAsync(id, request, ct);

    [HttpGet("medical-reports/{id:guid}/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.PrescriptionVerify)]
    public Task<MedicalReportVerificationDto> Verify(Guid id, [FromQuery] string? h, CancellationToken ct) =>
        reports.VerifyAsync(id, h, ct);
}

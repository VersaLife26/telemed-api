using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeleMed.Api.RateLimiting;
using TeleMed.Application.DoctorApplications;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Rules;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("doctor-applications")]
[AllowAnonymous]
public sealed class DoctorApplicationsController(DoctorApplicationService applications) : ControllerBase
{
    [HttpGet("eligibility")]
    [EnableRateLimiting(RateLimitingSetup.Eligibility)]
    public Task<EligibilityDto> Eligibility([FromQuery] EligibilityQuery query, CancellationToken ct) =>
        applications.GetEligibilityAsync(query, ct);

    [HttpPost]
    [EnableRateLimiting(RateLimitingSetup.DoctorApply)]
    [ProducesResponseType<DoctorApplicationCreatedDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> Apply(DoctorApplicationRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await applications.ApplyAsync(request, ct));

    [HttpPut("{id:guid}/documents/{type}")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting(RateLimitingSetup.DocumentUpload)]
    [RequestSizeLimit(PlatformPolicy.DoctorDocumentMaxBytes + (64 * 1024))]
    public async Task<DoctorDocumentDto> UploadDocument(
        Guid id, string type, IFormFile file, [FromHeader(Name = "X-Upload-Token")] string? uploadToken, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        return await applications.UploadDocumentAsync(id, type, uploadToken, content, file.FileName, ct);
    }
}

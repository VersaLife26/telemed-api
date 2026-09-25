using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Admin.Credentialing;
using TeleMed.Application.Common;
using TeleMed.Application.Doctors;
using TeleMed.Application.Permissions;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin/doctor-applications")]
[AdminAuthorize(AdminPermission.Credentialing)]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminDoctorApplicationsController(CredentialingService credentialing) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<DoctorApplicationSummaryDto>> List([FromQuery] DoctorApplicationQuery query, CancellationToken ct) =>
        credentialing.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<DoctorApplicationDto> Get(Guid id, CancellationToken ct) => credentialing.GetAsync(id, ct);

    [HttpGet("{id:guid}/documents/{documentId:guid}")]
    public Task<SignedUrlDto> GetDocument(Guid id, Guid documentId, CancellationToken ct) =>
        credentialing.GetDocumentUrlAsync(id, documentId, ct);

    [HttpPut("{id:guid}/checklist")]
    public Task<DoctorApplicationDto> UpdateChecklist(Guid id, UpdateChecklistRequest request, CancellationToken ct) =>
        credentialing.UpdateChecklistAsync(id, request, ct);

    [HttpPost("{id:guid}/start-review")]
    public Task<DoctorApplicationDto> StartReview(Guid id, CancellationToken ct) => credentialing.StartReviewAsync(id, ct);

    [HttpPost("{id:guid}/approve")]
    public Task<DoctorApplicationDto> Approve(Guid id, CancellationToken ct) => credentialing.ApproveAsync(id, ct);

    [HttpPost("{id:guid}/reject")]
    public Task<DoctorApplicationDto> Reject(Guid id, RejectApplicationRequest request, CancellationToken ct) =>
        credentialing.RejectAsync(id, request, ct);
}

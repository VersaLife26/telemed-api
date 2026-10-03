using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Admin.Doctors;
using TeleMed.Application.Common;
using TeleMed.Application.Doctors;
using TeleMed.Application.Permissions;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin/doctors")]
[AdminAuthorize(AdminPermission.Doctors)]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminDoctorsController(AdminDoctorService doctors) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AdminDoctorListItemDto>> List([FromQuery] AdminDoctorQuery query, CancellationToken ct) =>
        doctors.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<AdminDoctorDto> Get(Guid id, CancellationToken ct) => doctors.GetAsync(id, ct);

    [HttpGet("{id:guid}/documents/{documentId:guid}")]
    public Task<SignedUrlDto> GetDocument(Guid id, Guid documentId, CancellationToken ct) =>
        doctors.GetDocumentUrlAsync(id, documentId, ct);

    [HttpPost("{id:guid}/suspend")]
    public Task<AdminDoctorDto> Suspend(Guid id, SuspendDoctorRequest request, CancellationToken ct) =>
        doctors.SuspendAsync(id, request, ct);

    [HttpPost("{id:guid}/reinstate")]
    public Task<AdminDoctorDto> Reinstate(Guid id, CancellationToken ct) => doctors.ReinstateAsync(id, ct);

    [HttpPut("{id:guid}/foreign-multiplier")]
    public Task<AdminDoctorDto> SetForeignMultiplier(Guid id, SetForeignMultiplierRequest request, CancellationToken ct) =>
        doctors.SetForeignMultiplierAsync(id, request, ct);
}

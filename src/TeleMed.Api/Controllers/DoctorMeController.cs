using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Application.Doctors;
using TeleMed.Application.Users;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("doctors/me")]
[Authorize(Roles = UserRoleNames.Doctor)]
public sealed class DoctorMeController(DoctorSelfService self) : ControllerBase
{
    private const long UploadRequestLimit = PlatformPolicy.DoctorDocumentMaxBytes + (64 * 1024);

    [HttpGet]
    public Task<DoctorProfileDto> Get(CancellationToken ct) => self.GetAsync(ct);

    [HttpPut]
    public Task<DoctorProfileDto> Update(UpdateDoctorProfileRequest request, CancellationToken ct) => self.UpdateAsync(request, ct);

    [HttpGet("photo")]
    public Task<PhotoUrlDto> GetPhoto(CancellationToken ct) => self.GetPhotoUrlAsync(ct);

    [HttpPut("photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PlatformPolicy.ProfilePhotoMaxBytes + (64 * 1024))]
    public async Task<DoctorProfileDto> PutPhoto(IFormFile file, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        return await self.SetPhotoAsync(content, ct);
    }

    [HttpDelete("photo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> DeletePhoto(CancellationToken ct)
    {
        await self.DeletePhotoAsync(ct);
        return NoContent();
    }

    [HttpGet("signature")]
    public Task<SignedUrlDto> GetSignature(CancellationToken ct) => self.GetStampUrlAsync(DoctorDocumentType.Signature, ct);

    [HttpPut("signature")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadRequestLimit)]
    public Task<DoctorDocumentDto> PutSignature(IFormFile file, CancellationToken ct) => PutStampAsync(DoctorDocumentType.Signature, file, ct);

    [HttpGet("seal")]
    public Task<SignedUrlDto> GetSeal(CancellationToken ct) => self.GetStampUrlAsync(DoctorDocumentType.Seal, ct);

    [HttpPut("seal")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadRequestLimit)]
    public Task<DoctorDocumentDto> PutSeal(IFormFile file, CancellationToken ct) => PutStampAsync(DoctorDocumentType.Seal, file, ct);

    [HttpGet("documents")]
    public Task<IReadOnlyList<DoctorDocumentDto>> ListDocuments(CancellationToken ct) => self.ListDocumentsAsync(ct);

    [HttpPost("documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadRequestLimit)]
    [ProducesResponseType<DoctorDocumentDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> UploadDocument([FromForm] string type, IFormFile file, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        return StatusCode(StatusCodes.Status201Created, await self.UploadDocumentAsync(type, content, file.FileName, ct));
    }

    private async Task<DoctorDocumentDto> PutStampAsync(DoctorDocumentType type, IFormFile file, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        return await self.SetStampAsync(type, content, file.FileName, ct);
    }
}

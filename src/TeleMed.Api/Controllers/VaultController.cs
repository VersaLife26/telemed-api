using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeleMed.Api.RateLimiting;
using TeleMed.Application.Common;
using TeleMed.Application.Vault;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("vault")]
[Authorize(Roles = $"{UserRoleNames.Patient},{UserRoleNames.Doctor}")]
public sealed class VaultController(VaultService vault) : ControllerBase
{
    [HttpGet("documents")]
    public Task<PagedResult<VaultDocumentDto>> ListDocuments([FromQuery] VaultDocumentQuery query, CancellationToken ct) =>
        vault.ListDocumentsAsync(query, ct);

    [HttpPost("documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PlatformPolicy.VaultMaxDocumentBytes + (64 * 1024))]
    [EnableRateLimiting(RateLimitingSetup.DocumentUpload)]
    [ProducesResponseType<VaultDocumentDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> Upload([FromForm] VaultUploadForm form, IFormFile file, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        return StatusCode(StatusCodes.Status201Created, await vault.UploadAsync(form, content, file.FileName, ct));
    }

    [HttpGet("documents/{id:guid}")]
    public Task<VaultDocumentDto> GetDocument(Guid id, CancellationToken ct) => vault.GetDocumentAsync(id, ct);

    [HttpPatch("documents/{id:guid}")]
    public Task<VaultDocumentDto> UpdateDocument(Guid id, UpdateVaultDocumentRequest request, CancellationToken ct) =>
        vault.UpdateDocumentAsync(id, request, ct);

    [HttpDelete("documents/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> DeleteDocument(Guid id, CancellationToken ct)
    {
        await vault.DeleteDocumentAsync(id, ct);
        return NoContent();
    }

    [HttpGet("documents/{id:guid}/download")]
    public Task<VaultDownloadDto> Download(Guid id, CancellationToken ct) => vault.DownloadAsync(id, ct);

    [HttpGet("patients")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<IReadOnlyList<AccessiblePatientDto>> Patients(CancellationToken ct) => vault.ListAccessiblePatientsAsync(ct);

    [HttpGet("folders")]
    public Task<IReadOnlyList<VaultFolderDto>> ListFolders([FromQuery] VaultFolderQuery query, CancellationToken ct) => vault.ListFoldersAsync(query, ct);

    [HttpPost("folders")]
    [Authorize(Roles = UserRoleNames.Patient)]
    [ProducesResponseType<VaultFolderDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> CreateFolder(CreateVaultFolderRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await vault.CreateFolderAsync(request, ct));

    [HttpPatch("folders/{id:guid}")]
    [Authorize(Roles = UserRoleNames.Patient)]
    public Task<VaultFolderDto> UpdateFolder(Guid id, UpdateVaultFolderRequest request, CancellationToken ct) => vault.UpdateFolderAsync(id, request, ct);

    [HttpDelete("folders/{id:guid}")]
    [Authorize(Roles = UserRoleNames.Patient)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> DeleteFolder(Guid id, CancellationToken ct)
    {
        await vault.DeleteFolderAsync(id, ct);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Mvc;
using TeleMed.Application.Auth;
using TeleMed.Application.Users;
using TeleMed.Domain.Rules;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("me")]
public sealed class MeController(MeService me) : ControllerBase
{
    [HttpGet]
    public Task<MeDto> Get(CancellationToken ct) => me.GetAsync(ct);

    [HttpPut]
    public Task<MeDto> Update(UpdateMeRequest request, CancellationToken ct) => me.UpdateAsync(request, ct);

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> Delete(CancellationToken ct)
    {
        await me.DeleteAsync(ct);
        return NoContent();
    }

    [HttpPut("password")]
    public Task<AuthResponse> ChangePassword(ChangePasswordRequest request, CancellationToken ct) => me.ChangePasswordAsync(request, ct);

    [HttpGet("photo")]
    public Task<PhotoUrlDto> GetPhoto(CancellationToken ct) => me.GetPhotoUrlAsync(ct);

    [HttpPut("photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PlatformPolicy.ProfilePhotoMaxBytes + (64 * 1024))]
    public async Task<MeDto> PutPhoto(IFormFile file, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        return await me.SetPhotoAsync(content, ct);
    }

    [HttpDelete("photo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> DeletePhoto(CancellationToken ct)
    {
        await me.DeletePhotoAsync(ct);
        return NoContent();
    }
}

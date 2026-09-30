using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Admin.Users;
using TeleMed.Application.Common;
using TeleMed.Application.Permissions;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin/users")]
[AdminAuthorize(AdminPermission.Users)]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminPlatformUsersController(PlatformUserService users) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PlatformUserDto>> List([FromQuery] PlatformUserQuery query, CancellationToken ct) => users.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<PlatformUserDetailDto> Get(Guid id, CancellationToken ct) => users.GetAsync(id, ct);

    [HttpGet("{id:guid}/activity")]
    public Task<UserActivityDto> Activity(Guid id, CancellationToken ct) => users.GetActivityAsync(id, ct);

    [HttpPost("{id:guid}/suspend")]
    public Task<PlatformUserDetailDto> Suspend(Guid id, SuspendUserRequest request, CancellationToken ct) => users.SuspendAsync(id, request, ct);

    [HttpPost("{id:guid}/reinstate")]
    public Task<PlatformUserDetailDto> Reinstate(Guid id, CancellationToken ct) => users.ReinstateAsync(id, ct);

    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> ResetPassword(Guid id, ResetUserPasswordRequest request, CancellationToken ct)
    {
        await users.ResetPasswordAsync(id, request, ct);
        return NoContent();
    }
}

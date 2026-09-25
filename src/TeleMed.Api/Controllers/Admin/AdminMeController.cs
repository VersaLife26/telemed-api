using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Admin.AdminUsers;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin")]
[AdminAuthorize]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminMeController(AdminUserService adminUsers) : ControllerBase
{
    [HttpGet("me")]
    public Task<AdminMeDto> Me(CancellationToken ct) => adminUsers.GetMeAsync(ct);

    [HttpGet("permissions")]
    public IReadOnlyList<PermissionRolesDto> Permissions() => AdminUserService.GetPermissionMatrix();
}

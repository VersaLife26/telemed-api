using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Admin.AdminUsers;
using TeleMed.Application.Permissions;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin/admin-users")]
[AdminAuthorize(AdminPermission.AdminUsers)]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminUsersController(AdminUserService adminUsers) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AdminUserDto>> List(CancellationToken ct) => adminUsers.ListAsync(ct);

    [HttpPost]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> Create(CreateAdminUserRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await adminUsers.CreateAsync(request, ct));

    [HttpPatch("{id:guid}")]
    public Task<AdminUserDto> Update(Guid id, UpdateAdminUserRequest request, CancellationToken ct) => adminUsers.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/deactivate")]
    public Task<AdminUserDto> Deactivate(Guid id, CancellationToken ct) => adminUsers.DeactivateAsync(id, ct);
}

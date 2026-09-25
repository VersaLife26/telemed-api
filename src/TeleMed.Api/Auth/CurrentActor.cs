using System.Security.Claims;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Auth;

internal sealed class CurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public Guid? UserId => Guid.TryParse(AuthenticatedUser?.FindFirstValue(JwtAuthentication.SubjectClaim), out var id) ? id : null;

    public UserRole? Role => UserRoleNames.Parse(AuthenticatedUser?.FindFirstValue(JwtAuthentication.RoleClaim));

    public AdminActor? Admin =>
        AuthenticatedUser is { } user
        && Guid.TryParse(user.FindFirstValue(AdminAuthentication.AdminIdClaim), out var id)
        && Enum.TryParse<AdminRole>(user.FindFirstValue(AdminAuthentication.AdminRoleClaim), out var role)
        && user.FindFirstValue(AdminAuthentication.EmailClaim) is { } email
            ? new AdminActor(id, email, role)
            : null;

    private ClaimsPrincipal? AuthenticatedUser =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;
}

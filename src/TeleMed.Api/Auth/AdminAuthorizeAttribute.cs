using Microsoft.AspNetCore.Authorization;
using TeleMed.Application.Permissions;

namespace TeleMed.Api.Auth;

internal sealed class AdminAuthorizeAttribute : AuthorizeAttribute
{
    public AdminAuthorizeAttribute()
    {
        AuthenticationSchemes = AdminAuthentication.Scheme;
        Policy = AdminAuthentication.AnyAdminPolicy;
    }

    public AdminAuthorizeAttribute(AdminPermission permission)
        : this()
    {
        Policy = AdminAuthentication.PolicyFor(permission);
    }
}

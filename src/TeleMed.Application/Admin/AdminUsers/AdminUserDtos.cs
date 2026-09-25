using TeleMed.Application.Permissions;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.AdminUsers;

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    AdminRole Role,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    Guid? CreatedByAdminId,
    DateTimeOffset CreatedAt);

public sealed record AdminMeDto(Guid Id, string Email, string DisplayName, AdminRole Role, IReadOnlyList<AdminPermission> Permissions);

public sealed record PermissionRolesDto(AdminPermission Permission, IReadOnlyList<AdminRole> Roles);

public sealed record CreateAdminUserRequest(string Email, string DisplayName, AdminRole Role);

public sealed record UpdateAdminUserRequest(string? DisplayName, AdminRole? Role, bool? IsActive);

using TeleMed.Domain.Enums;

namespace TeleMed.Application.Permissions;

public static class PermissionMatrix
{
    private static readonly AdminRole[] AllRoles = Enum.GetValues<AdminRole>();

    public static readonly IReadOnlyDictionary<AdminPermission, IReadOnlyList<AdminRole>> Roles =
        new Dictionary<AdminPermission, IReadOnlyList<AdminRole>>
        {
            [AdminPermission.Credentialing] = AllRoles,
            [AdminPermission.Doctors] = AllRoles,
            [AdminPermission.Users] = AllRoles,
            [AdminPermission.Appointments] = AllRoles,
            [AdminPermission.Content] = AllRoles,
            [AdminPermission.Disputes] = AllRoles,
            [AdminPermission.Analytics] = AllRoles,
            [AdminPermission.Audit] = AllRoles,
            [AdminPermission.Finance] = [AdminRole.Finance, AdminRole.SuperAdmin],
            [AdminPermission.AuditExport] = [AdminRole.Finance, AdminRole.SuperAdmin],
            [AdminPermission.AdminUsers] = [AdminRole.SuperAdmin],
            [AdminPermission.CardHold] = [AdminRole.SuperAdmin],
        };

    public static bool Allows(AdminRole role, AdminPermission permission) =>
        Roles.TryGetValue(permission, out var roles) && roles.Contains(role);

    public static IReadOnlyList<AdminPermission> For(AdminRole role) =>
        Enum.GetValues<AdminPermission>().Where(p => Allows(role, p)).ToList();
}

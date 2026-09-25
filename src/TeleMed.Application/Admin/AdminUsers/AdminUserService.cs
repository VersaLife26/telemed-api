using TeleMed.Application.Abstractions;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Permissions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.AdminUsers;

public sealed class AdminUserService(
    ICurrentActor actor,
    IAdminUserRepository repository,
    IAdminDirectory directory,
    IUnitOfWork unitOfWork)
{
    public async Task<AdminMeDto> GetMeAsync(CancellationToken ct)
    {
        var admin = await repository.FindAsync(actor.RequireAdmin().Id, ct) ?? throw new NotFoundException("Admin user not found.");
        return new AdminMeDto(admin.Id, admin.Email, admin.DisplayName, admin.Role, PermissionMatrix.For(admin.Role));
    }

    public static IReadOnlyList<PermissionRolesDto> GetPermissionMatrix() =>
        PermissionMatrix.Roles.Select(p => new PermissionRolesDto(p.Key, p.Value)).ToList();

    public async Task<IReadOnlyList<AdminUserDto>> ListAsync(CancellationToken ct) =>
        (await repository.ListAsync(ct)).Select(a => a.ToDto()).ToList();

    public async Task<AdminUserDto> CreateAsync(CreateAdminUserRequest request, CancellationToken ct)
    {
        var admin = new AdminUser
        {
            Email = NormalizeEmail(request.Email),
            DisplayName = request.DisplayName.Trim(),
            Role = request.Role,
            CreatedByAdminId = actor.RequireAdmin().Id,
        };
        repository.Add(admin);
        await unitOfWork.SaveChangesAsync(ct);
        directory.Evict(admin.Email);
        return admin.ToDto();
    }

    public Task<AdminUserDto> UpdateAsync(Guid id, UpdateAdminUserRequest request, CancellationToken ct) =>
        ChangeAsync(id, request.DisplayName, request.Role, request.IsActive, ct);

    public Task<AdminUserDto> DeactivateAsync(Guid id, CancellationToken ct) => ChangeAsync(id, null, null, false, ct);

    public async Task BootstrapSuperAdminAsync(string email, CancellationToken ct)
    {
        if (await repository.AnyAsync(ct))
        {
            return;
        }

        var normalized = NormalizeEmail(email);
        repository.Add(new AdminUser { Email = normalized, DisplayName = normalized.Split('@')[0], Role = AdminRole.SuperAdmin });
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConflictException)
        {
            // Another instance bootstrapped the same account concurrently.
        }
    }

    private async Task<AdminUserDto> ChangeAsync(Guid id, string? displayName, AdminRole? role, bool? isActive, CancellationToken ct)
    {
        var self = actor.RequireAdmin();
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var activeSuperAdmins = await repository.LockActiveSuperAdminsAsync(ct);
        var admin = await LoadAsync(id, ct);

        var losesSuperAdmin = admin is { Role: AdminRole.SuperAdmin, IsActive: true }
            && ((role is { } r && r != AdminRole.SuperAdmin) || isActive == false);
        var changesAccess = (role is { } newRole && newRole != admin.Role) || (isActive is { } active && active != admin.IsActive);
        if (admin.Id == self.Id && changesAccess)
        {
            throw new ForbiddenException("You cannot change your own role or deactivate yourself.", "self_access_change");
        }

        if (losesSuperAdmin && activeSuperAdmins <= 1)
        {
            throw new ConflictException("last_super_admin", "The last active super admin cannot be demoted or deactivated.");
        }

        if (displayName is not null)
        {
            admin.DisplayName = displayName.Trim();
        }

        admin.Role = role ?? admin.Role;
        admin.IsActive = isActive ?? admin.IsActive;
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        directory.Evict(admin.Email);
        return admin.ToDto();
    }

    private async Task<AdminUser> LoadAsync(Guid id, CancellationToken ct) =>
        await repository.FindAsync(id, ct) ?? throw new NotFoundException("Admin user not found.");

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

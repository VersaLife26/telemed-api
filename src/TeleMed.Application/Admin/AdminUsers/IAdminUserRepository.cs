using TeleMed.Domain.Entities;

namespace TeleMed.Application.Admin.AdminUsers;

public interface IAdminUserRepository
{
    void Add(AdminUser admin);
    Task<AdminUser?> FindAsync(Guid id, CancellationToken ct);
    Task<AdminUser?> FindByEmailAsync(string email, CancellationToken ct);
    Task<IReadOnlyList<AdminUser>> ListAsync(CancellationToken ct);
    Task<bool> AnyAsync(CancellationToken ct);

    // Row-locks the active super_admins until the surrounding transaction ends, so concurrent demotions cannot both pass the last-one check.
    Task<int> LockActiveSuperAdminsAsync(CancellationToken ct);
}

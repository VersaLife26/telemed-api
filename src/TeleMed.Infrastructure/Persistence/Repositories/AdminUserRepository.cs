using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Admin.AdminUsers;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class AdminUserRepository(AppDbContext db) : IAdminUserRepository
{
    public void Add(AdminUser admin) => db.AdminUsers.Add(admin);

    public Task<AdminUser?> FindAsync(Guid id, CancellationToken ct) => db.AdminUsers.SingleOrDefaultAsync(a => a.Id == id, ct);

    public Task<AdminUser?> FindByEmailAsync(string email, CancellationToken ct) =>
        db.AdminUsers.SingleOrDefaultAsync(a => a.Email == email, ct);

    public async Task<IReadOnlyList<AdminUser>> ListAsync(CancellationToken ct) =>
        await db.AdminUsers.AsNoTracking().OrderBy(a => a.Email).ToListAsync(ct);

    public Task<bool> AnyAsync(CancellationToken ct) => db.AdminUsers.AnyAsync(ct);

    public async Task<int> LockActiveSuperAdminsAsync(CancellationToken ct) =>
        (await db.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM admin_users WHERE role = 'super_admin' AND is_active FOR UPDATE")
            .ToListAsync(ct)).Count;
}

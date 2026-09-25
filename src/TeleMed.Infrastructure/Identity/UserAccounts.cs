using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Infrastructure.Persistence;

namespace TeleMed.Infrastructure.Identity;

internal sealed class UserAccounts(UserManager<User> users, AppDbContext db, SessionValidator sessions) : IUserAccounts
{
    private static readonly Lazy<string> DummyPasswordHash = new(() => new PasswordHasher<User>().HashPassword(new User(), Guid.NewGuid().ToString()));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var normalized = users.NormalizeEmail(email);
        return db.Users.Where(u => u.NormalizedEmail == normalized)
            .OrderBy(u => u.Status == UserStatus.Deleted)
            .FirstOrDefaultAsync(ct);
    }

    public Task<User?> FindByPhoneAsync(string phoneNumber, CancellationToken ct) =>
        db.Users.Where(u => u.PhoneNumber == phoneNumber)
            .OrderBy(u => u.Status == UserStatus.Deleted)
            .FirstOrDefaultAsync(ct);

    public Task<User?> FindByLoginAsync(string provider, string key, CancellationToken ct) =>
        (from login in db.UserLogins
         join user in db.Users on login.UserId equals user.Id
         where login.LoginProvider == provider && login.ProviderKey == key
         select user).FirstOrDefaultAsync(ct);

    public async Task CreateAsync(User user, string? password)
    {
        user.UserName ??= user.Id.ToString("N");
        var result = password is null ? await users.CreateAsync(user) : await users.CreateAsync(user, password);
        ThrowIfFailed(result, "password");
    }

    public string HashPassword(string password) => users.PasswordHasher.HashPassword(new User(), password);

    public void AddLogin(User user, string provider, string key) =>
        db.UserLogins.Add(new IdentityUserLogin<Guid>
        {
            UserId = user.Id,
            LoginProvider = provider,
            ProviderKey = key,
            ProviderDisplayName = provider,
        });

    public async Task<PasswordCheck> CheckPasswordAsync(User? user, string password)
    {
        if (user?.PasswordHash is null)
        {
            // Hash anyway so an unknown email costs the same time as a wrong password.
            users.PasswordHasher.VerifyHashedPassword(new User(), DummyPasswordHash.Value, password);
            return PasswordCheck.Failed;
        }

        if (await users.IsLockedOutAsync(user))
        {
            return PasswordCheck.LockedOut;
        }

        if (await users.CheckPasswordAsync(user, password))
        {
            await users.ResetAccessFailedCountAsync(user);
            return PasswordCheck.Success;
        }

        await users.AccessFailedAsync(user);
        return await users.IsLockedOutAsync(user) ? PasswordCheck.LockedOut : PasswordCheck.Failed;
    }

    public async Task SetPasswordAsync(User user, string? currentPassword, string newPassword)
    {
        IdentityResult result;
        if (user.PasswordHash is null)
        {
            result = await users.AddPasswordAsync(user, newPassword);
        }
        else if (string.IsNullOrEmpty(currentPassword))
        {
            throw new ValidationException([new ValidationFailure("currentPassword", "The current password is required.")]);
        }
        else
        {
            result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        }

        ThrowIfFailed(result, "newPassword");
        sessions.Evict(user.Id);
    }

    public async Task RemovePasswordAsync(User user)
    {
        ThrowIfFailed(await users.RemovePasswordAsync(user), "user");
        sessions.Evict(user.Id);
    }

    public async Task UpdateAsync(User user)
    {
        ThrowIfFailed(await users.UpdateAsync(user), "user");
        sessions.Evict(user.Id);
    }

    public async Task UpdateSecurityStampAsync(User user)
    {
        ThrowIfFailed(await users.UpdateSecurityStampAsync(user), "user");
        sessions.Evict(user.Id);
    }

    public async Task<IReadOnlyList<User>> ListDueForErasureAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        await db.Users
            .Where(u => u.Status == UserStatus.Deleted && u.ErasureDueAt <= now && u.AnonymizedAt == null)
            .OrderBy(u => u.ErasureDueAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task RemoveLoginsAsync(User user, CancellationToken ct) =>
        db.UserLogins.RemoveRange(await db.UserLogins.Where(l => l.UserId == user.Id).ToListAsync(ct));

    private static void ThrowIfFailed(IdentityResult result, string property)
    {
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e =>
                new ValidationFailure(e.Code == nameof(IdentityErrorDescriber.PasswordMismatch) ? "currentPassword" : property, e.Description)));
        }
    }
}

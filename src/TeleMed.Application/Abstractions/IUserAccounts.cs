using TeleMed.Domain.Entities;

namespace TeleMed.Application.Abstractions;

public interface IUserAccounts
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);
    Task<User?> FindByPhoneAsync(string phoneNumber, CancellationToken ct);
    Task<User?> FindByLoginAsync(string provider, string key, CancellationToken ct);

    Task CreateAsync(User user, string? password);
    string HashPassword(string password);
    void AddLogin(User user, string provider, string key);
    Task<PasswordCheck> CheckPasswordAsync(User? user, string password);
    Task SetPasswordAsync(User user, string? currentPassword, string newPassword);
    Task RemovePasswordAsync(User user);
    Task UpdateAsync(User user);
    Task UpdateSecurityStampAsync(User user);

    // Tracked; deleted accounts whose erasure grace period has passed and that are not yet anonymised.
    Task<IReadOnlyList<User>> ListDueForErasureAsync(DateTimeOffset now, int limit, CancellationToken ct);
    Task RemoveLoginsAsync(User user, CancellationToken ct);
}

public enum PasswordCheck
{
    Success,
    Failed,
    LockedOut,
}

using TeleMed.Domain.Entities;

namespace TeleMed.Application.Auth;

public interface IPasswordResetTokenRepository
{
    void Add(PasswordResetToken token);
    Task<int> CountCreatedSinceAsync(string email, DateTimeOffset since, CancellationToken ct);
    Task<IReadOnlyList<PasswordResetToken>> ListActiveByEmailAsync(string email, DateTimeOffset now, CancellationToken ct);
    Task<PasswordResetToken?> FindByHashForUpdateAsync(string tokenHash, CancellationToken ct);
    Task<IReadOnlyList<PasswordResetToken>> ListCreatedBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken ct);
    void RemoveRange(IEnumerable<PasswordResetToken> tokens);
}

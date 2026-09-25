using TeleMed.Domain.Entities;

namespace TeleMed.Application.Auth;

public interface IRefreshTokenRepository
{
    void Add(RefreshToken token);
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct);
    Task<RefreshToken?> FindByHashForUpdateAsync(string tokenHash, CancellationToken ct);
    Task<IReadOnlyList<RefreshToken>> ListActiveByFamilyAsync(Guid familyId, CancellationToken ct);
    Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(Guid userId, CancellationToken ct);
    // Expired or revoked before the cutoff; tracked.
    Task<IReadOnlyList<RefreshToken>> ListPurgeableAsync(DateTimeOffset cutoff, int limit, CancellationToken ct);
    void RemoveRange(IEnumerable<RefreshToken> tokens);
}

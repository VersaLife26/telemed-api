using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Auth;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public Task<RefreshToken?> FindByHashForUpdateAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens
            .FromSql($"SELECT * FROM refresh_tokens WHERE token_hash = {tokenHash} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<RefreshToken>> ListActiveByFamilyAsync(Guid familyId, CancellationToken ct) =>
        await db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(ct);

    public async Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(Guid userId, CancellationToken ct) =>
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);

    public async Task<IReadOnlyList<RefreshToken>> ListPurgeableAsync(DateTimeOffset cutoff, int limit, CancellationToken ct) =>
        await db.RefreshTokens.Where(t => t.ExpiresAt < cutoff || t.RevokedAt < cutoff).OrderBy(t => t.Id).Take(limit).ToListAsync(ct);

    public void RemoveRange(IEnumerable<RefreshToken> tokens) => db.RefreshTokens.RemoveRange(tokens);
}

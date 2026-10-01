using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Auth;
using TeleMed.Domain.Entities;
using TeleMed.Infrastructure.Persistence;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class PasswordResetTokenRepository(AppDbContext db) : IPasswordResetTokenRepository
{
    public void Add(PasswordResetToken token) => db.PasswordResetTokens.Add(token);

    public Task<int> CountCreatedSinceAsync(string email, DateTimeOffset since, CancellationToken ct) =>
        db.PasswordResetTokens.CountAsync(t => t.Email == email && t.CreatedAt > since, ct);

    public async Task<IReadOnlyList<PasswordResetToken>> ListActiveByEmailAsync(string email, DateTimeOffset now, CancellationToken ct) =>
        await db.PasswordResetTokens
            .Where(t => t.Email == email && t.ConsumedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);

    public Task<PasswordResetToken?> FindByHashForUpdateAsync(string tokenHash, CancellationToken ct) =>
        db.PasswordResetTokens
            .FromSql($"SELECT * FROM password_reset_tokens WHERE token_hash = {tokenHash} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<PasswordResetToken>> ListCreatedBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken ct) =>
        await db.PasswordResetTokens.Where(t => t.CreatedAt < cutoff).OrderBy(t => t.Id).Take(limit).ToListAsync(ct);

    public void RemoveRange(IEnumerable<PasswordResetToken> tokens) => db.PasswordResetTokens.RemoveRange(tokens);
}

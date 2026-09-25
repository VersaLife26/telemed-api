using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Auth;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class OtpChallengeRepository(AppDbContext db) : IOtpChallengeRepository
{
    public void Add(OtpChallenge challenge) => db.OtpChallenges.Add(challenge);

    public Task<int> CountCreatedSinceAsync(string destination, DateTimeOffset since, CancellationToken ct) =>
        db.OtpChallenges.CountAsync(c => c.Destination == destination && c.CreatedAt > since, ct);

    public Task<OtpChallenge?> FindLatestActiveForUpdateAsync(string destination, DateTimeOffset now, CancellationToken ct) =>
        db.OtpChallenges
            .FromSql($"""
                SELECT * FROM otp_challenges
                WHERE destination = {destination} AND consumed_at IS NULL AND expires_at > {now}
                ORDER BY created_at DESC
                LIMIT 1
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<OtpChallenge>> ListCreatedBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken ct) =>
        await db.OtpChallenges.Where(c => c.CreatedAt < cutoff).OrderBy(c => c.Id).Take(limit).ToListAsync(ct);

    public void RemoveRange(IEnumerable<OtpChallenge> challenges) => db.OtpChallenges.RemoveRange(challenges);
}

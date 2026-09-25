using TeleMed.Domain.Entities;

namespace TeleMed.Application.Auth;

public interface IOtpChallengeRepository
{
    void Add(OtpChallenge challenge);
    Task<int> CountCreatedSinceAsync(string destination, DateTimeOffset since, CancellationToken ct);
    Task<OtpChallenge?> FindLatestActiveForUpdateAsync(string destination, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<OtpChallenge>> ListCreatedBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken ct);
    void RemoveRange(IEnumerable<OtpChallenge> challenges);
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TeleMed.Domain.Enums;
using TeleMed.Infrastructure.Options;
using TeleMed.Infrastructure.Persistence;

namespace TeleMed.Infrastructure.Identity;

public sealed class SessionValidator(
    IServiceScopeFactory scopes,
    IMemoryCache cache,
    IOptions<JwtOptions> options,
    TimeProvider time)
{
    public async Task<bool> IsValidAsync(Guid userId, string? securityStamp, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (!cache.TryGetValue<Entry>(Key(userId), out var entry) || entry!.FreshUntil <= now)
        {
            await using var scope = scopes.CreateAsyncScope();
            var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.SecurityStamp, u.Status })
                .SingleOrDefaultAsync(ct);
            var lifetime = options.Value.SessionCacheDuration;
            entry = new Entry(row?.SecurityStamp, row?.Status == UserStatus.Active, now + lifetime);
            cache.Set(Key(userId), entry, lifetime);
        }

        return entry.IsActive && entry.SecurityStamp is not null && entry.SecurityStamp == securityStamp;
    }

    public void Evict(Guid userId) => cache.Remove(Key(userId));

    private static string Key(Guid userId) => $"session:{userId}";

    private sealed record Entry(string? SecurityStamp, bool IsActive, DateTimeOffset FreshUntil);
}

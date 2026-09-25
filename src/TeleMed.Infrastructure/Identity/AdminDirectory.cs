using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Admin.AdminUsers;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Identity;

internal sealed class AdminDirectory(
    IServiceScopeFactory scopes,
    IMemoryCache cache,
    IOptions<AdminAuthOptions> options,
    TimeProvider time) : IAdminDirectory
{
    public async Task<AdminActor?> FindActiveAsync(string email, CancellationToken ct)
    {
        var key = Key(email);
        var now = time.GetUtcNow();
        if (cache.TryGetValue<Entry>(key, out var entry) && entry!.FreshUntil > now)
        {
            return entry.Admin;
        }

        await using var scope = scopes.CreateAsyncScope();
        var admin = await scope.ServiceProvider.GetRequiredService<IAdminUserRepository>().FindByEmailAsync(Normalize(email), ct);
        AdminActor? actor = null;
        if (admin is { IsActive: true })
        {
            admin.LastLoginAt = now;
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
            actor = new AdminActor(admin.Id, admin.Email, admin.Role);
        }

        var lifetime = options.Value.CacheDuration;
        cache.Set(key, new Entry(actor, now + lifetime), lifetime);
        return actor;
    }

    public void Evict(string email) => cache.Remove(Key(email));

    private static string Key(string email) => $"admin:{Normalize(email)}";

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private sealed record Entry(AdminActor? Admin, DateTimeOffset FreshUntil);
}

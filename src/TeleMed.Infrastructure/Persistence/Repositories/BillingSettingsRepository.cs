using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class BillingSettingsRepository(AppDbContext db) : IBillingSettingsRepository
{
    public Task<PlatformBillingSettings> GetAsync(CancellationToken ct) =>
        db.PlatformBillingSettings.SingleAsync(ct);
}

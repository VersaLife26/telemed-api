using TeleMed.Domain.Entities;

namespace TeleMed.Application.Abstractions;

public interface IBillingSettingsRepository
{
    Task<PlatformBillingSettings> GetAsync(CancellationToken ct);
}

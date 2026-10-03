using TeleMed.Domain.Entities;

namespace TeleMed.Application.Admin.Finance;

public interface ICommissionPolicy
{
    Task<int> GetDefaultBpsAsync(CancellationToken ct);
    Task<int> GetEffectiveBpsAsync(Guid doctorId, CancellationToken ct);
    Task<PlatformCommissionPolicy> GetForUpdateAsync(CancellationToken ct);
    Task<IReadOnlyList<DoctorCommissionDto>> ListDoctorRatesAsync(CancellationToken ct);
}

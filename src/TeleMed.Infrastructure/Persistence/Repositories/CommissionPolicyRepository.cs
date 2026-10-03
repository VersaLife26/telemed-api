using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Admin.Finance;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class CommissionPolicyRepository(AppDbContext db) : ICommissionPolicy
{
    public async Task<int> GetDefaultBpsAsync(CancellationToken ct) =>
        await db.PlatformCommissionPolicies.AsNoTracking()
            .Select(p => (int?)p.DefaultCommissionBps)
            .SingleOrDefaultAsync(ct)
        ?? PlatformPolicy.CommissionBps;

    public async Task<int> GetEffectiveBpsAsync(Guid doctorId, CancellationToken ct)
    {
        var overrideBps = await db.Doctors.AsNoTracking()
            .Where(d => d.Id == doctorId)
            .Select(d => d.CommissionBps)
            .SingleOrDefaultAsync(ct);
        return overrideBps ?? await GetDefaultBpsAsync(ct);
    }

    public async Task<PlatformCommissionPolicy> GetForUpdateAsync(CancellationToken ct)
    {
        var policy = await db.PlatformCommissionPolicies.SingleOrDefaultAsync(p => p.Id == PlatformCommissionPolicy.SingletonId, ct);
        if (policy is not null)
        {
            return policy;
        }

        policy = new PlatformCommissionPolicy
        {
            Id = PlatformCommissionPolicy.SingletonId,
            DefaultCommissionBps = PlatformPolicy.CommissionBps,
        };
        db.PlatformCommissionPolicies.Add(policy);
        return policy;
    }

    public async Task<IReadOnlyList<DoctorCommissionDto>> ListDoctorRatesAsync(CancellationToken ct) =>
        await db.Doctors.AsNoTracking()
            .Where(d => d.CommissionBps != null)
            .OrderBy(d => d.DisplayName).ThenBy(d => d.Id)
            .Select(d => new DoctorCommissionDto(d.Id, d.DisplayName, d.SlmcNumber, d.CommissionBps!.Value))
            .ToListAsync(ct);
}

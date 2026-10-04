using TeleMed.Application.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Admin.Finance;

public sealed class BillingSettingsService(IBillingSettingsRepository billing, IUnitOfWork unitOfWork)
{
    public async Task<BillingSettingsDto> GetAsync(CancellationToken ct) => Map(await billing.GetAsync(ct));

    public async Task<BillingSettingsDto> UpdateAsync(UpdateBillingSettingsRequest request, CancellationToken ct)
    {
        var row = await billing.GetAsync(ct);
        row.LkrPerUsd = request.LkrPerUsd;
        await unitOfWork.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<BillingSettingsDto> UpdateCardHoldAsync(UpdateCardHoldRequest request, CancellationToken ct)
    {
        var row = await billing.GetAsync(ct);
        row.HoldLkrWithinSixDays = request.HoldLkrWithinSixDays;
        row.HoldUsdWithinSixDays = request.HoldUsdWithinSixDays;
        await unitOfWork.SaveChangesAsync(ct);
        return Map(row);
    }

    private static BillingSettingsDto Map(PlatformBillingSettings row) =>
        new(row.LkrPerUsd, row.HoldLkrWithinSixDays, row.HoldUsdWithinSixDays);
}

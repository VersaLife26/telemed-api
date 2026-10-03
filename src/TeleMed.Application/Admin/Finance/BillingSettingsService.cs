using TeleMed.Application.Abstractions;

namespace TeleMed.Application.Admin.Finance;

public sealed class BillingSettingsService(IBillingSettingsRepository billing, IUnitOfWork unitOfWork)
{
    public async Task<BillingSettingsDto> GetAsync(CancellationToken ct) =>
        new((await billing.GetAsync(ct)).LkrPerUsd);

    public async Task<BillingSettingsDto> UpdateAsync(UpdateBillingSettingsRequest request, CancellationToken ct)
    {
        var row = await billing.GetAsync(ct);
        row.LkrPerUsd = request.LkrPerUsd;
        await unitOfWork.SaveChangesAsync(ct);
        return new BillingSettingsDto(row.LkrPerUsd);
    }
}

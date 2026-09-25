using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Finance;

// Read models over payments and refunds; test appointments never appear.
public interface IFinanceRepository
{
    Task<(IReadOnlyList<LedgerEntryDto> Items, long Total)> ListLedgerAsync(LedgerFilter filter, int skip, int take, CancellationToken ct);
    Task<LedgerTotalsDto> SumLedgerAsync(LedgerFilter filter, CancellationToken ct);
    IAsyncEnumerable<LedgerEntryDto> StreamLedgerAsync(LedgerFilter filter, CancellationToken ct);

    Task<(IReadOnlyList<AdminRefundDto> Items, long Total)> ListRefundsAsync(RefundStatus? status, int skip, int take, CancellationToken ct);
    Task<AdminRefundDto?> FindRefundAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AdminRefundDto>> ListRefundsForDisputeAsync(Guid disputeId, CancellationToken ct);

    void AddPromoCode(PromoCode promoCode);
    Task<PromoCode?> FindPromoCodeForUpdateAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<PromoCodeDto> Items, long Total)> ListPromoCodesAsync(bool? isActive, int skip, int take, CancellationToken ct);
    Task<PromoCodeDto?> FindPromoCodeAsync(Guid id, CancellationToken ct);
}

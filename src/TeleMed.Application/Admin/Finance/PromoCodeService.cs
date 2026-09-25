using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Admin.Finance;

// Discount terms are fixed once created, since live reservations were priced with them; only limits, validity and the active flag change.
public sealed class PromoCodeService(IFinanceRepository finance, IUnitOfWork unitOfWork, TimeProvider time)
{
    public async Task<PagedResult<PromoCodeDto>> ListAsync(PromoCodeQuery query, CancellationToken ct)
    {
        var (items, total) = await finance.ListPromoCodesAsync(query.IsActive, query.Skip, query.PageSize, ct);
        return new PagedResult<PromoCodeDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<PromoCodeDto> CreateAsync(CreatePromoCodeRequest request, CancellationToken ct)
    {
        var promo = new PromoCode
        {
            Code = PromoDiscount.Normalize(request.Code),
            Description = request.Description?.Trim() ?? "",
            DiscountType = request.DiscountType,
            PercentBps = request.PercentBps,
            AmountOffCents = request.AmountOffCents,
            MaxDiscountCents = request.MaxDiscountCents,
            MinAmountCents = request.MinAmountCents,
            ValidFrom = request.ValidFrom ?? time.GetUtcNow(),
            ValidUntil = request.ValidUntil,
            MaxRedemptions = request.MaxRedemptions,
            MaxPerUser = request.MaxPerUser,
        };
        if (promo.ValidUntil <= promo.ValidFrom)
        {
            throw new BadRequestException("invalid_validity", "validUntil must be after validFrom.");
        }

        finance.AddPromoCode(promo);
        await unitOfWork.SaveChangesAsync(ct);
        return await DtoAsync(promo.Id, ct);
    }

    public async Task<PromoCodeDto> UpdateAsync(Guid id, UpdatePromoCodeRequest request, CancellationToken ct)
    {
        var promo = await LoadAsync(id, ct);
        if (request.Description is not null)
        {
            promo.Description = request.Description.Trim();
        }

        if (request.ValidUntil is { } validUntil)
        {
            if (validUntil <= promo.ValidFrom)
            {
                throw new BadRequestException("invalid_validity", "validUntil must be after validFrom.");
            }

            promo.ValidUntil = validUntil;
        }

        promo.MaxRedemptions = request.MaxRedemptions ?? promo.MaxRedemptions;
        promo.MaxPerUser = request.MaxPerUser ?? promo.MaxPerUser;
        promo.IsActive = request.IsActive ?? promo.IsActive;
        await unitOfWork.SaveChangesAsync(ct);
        return await DtoAsync(id, ct);
    }

    public async Task<PromoCodeDto> DeactivateAsync(Guid id, CancellationToken ct)
    {
        var promo = await LoadAsync(id, ct);
        promo.IsActive = false;
        await unitOfWork.SaveChangesAsync(ct);
        return await DtoAsync(id, ct);
    }

    private async Task<PromoCode> LoadAsync(Guid id, CancellationToken ct) =>
        await finance.FindPromoCodeForUpdateAsync(id, ct) ?? throw new NotFoundException("Promo code not found.");

    private async Task<PromoCodeDto> DtoAsync(Guid id, CancellationToken ct) =>
        await finance.FindPromoCodeAsync(id, ct) ?? throw new NotFoundException("Promo code not found.");
}

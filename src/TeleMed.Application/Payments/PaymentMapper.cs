using Riok.Mapperly.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Payments;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class PaymentMapper
{
    public static partial RefundDto ToDto(this Refund refund);

    public static PaymentDto ToDto(this Payment p, IEnumerable<Refund> refunds) => new(
        p.Id,
        p.AppointmentId,
        p.DoctorId,
        p.Status,
        p.Provider,
        p.GrossCents,
        p.DiscountCents,
        p.AmountCents,
        p.CapturedCents,
        p.RefundedCents,
        p.Currency,
        p.PromoCode,
        p.AuthorizeOnly,
        p.AuthorizedAt,
        p.SucceededAt,
        p.CreatedAt,
        refunds.Where(r => r.PaymentId == p.Id).OrderBy(r => r.CreatedAt).Select(r => r.ToDto()).ToList());
}

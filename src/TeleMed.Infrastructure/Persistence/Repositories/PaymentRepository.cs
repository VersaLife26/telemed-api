using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Payments;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class PaymentRepository(AppDbContext db) : IPaymentRepository
{
    public void Add(Payment payment) => db.Payments.Add(payment);

    public Task<Payment?> FindAsync(Guid id, CancellationToken ct) => db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);

    public Task<Payment?> FindByAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.AppointmentId == appointmentId, ct);

    public async Task<Payment?> LockAsync(Guid id, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM payments WHERE id = {id} FOR UPDATE", ct);
        return await ReloadedAsync(db.Payments, id, ct);
    }

    public async Task<(IReadOnlyList<Payment> Items, long Total)> ListForPatientAsync(Guid patientId, int skip, int take, CancellationToken ct)
    {
        var query = db.Payments.AsNoTracking().Where(p => p.PatientId == patientId);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public void AddRefund(Refund refund) => db.Refunds.Add(refund);

    public async Task<IReadOnlyList<Refund>> ListRefundsAsync(IReadOnlyCollection<Guid> paymentIds, CancellationToken ct) =>
        await db.Refunds.AsNoTracking().Where(r => paymentIds.Contains(r.PaymentId)).ToListAsync(ct);

    public Task<Refund?> FindRefundForUpdateAsync(Guid id, CancellationToken ct) => ReloadedAsync(db.Refunds, id, ct);

    public Task<long> SumOpenRefundsAsync(Guid paymentId, CancellationToken ct) =>
        db.Refunds
            .Where(r => r.PaymentId == paymentId
                && (r.Status == RefundStatus.Requested || r.Status == RefundStatus.Approved || r.Status == RefundStatus.Processing || r.Status == RefundStatus.ManualRequired))
            .SumAsync(r => r.AmountCents, ct);

    public Task<PromoCode?> FindPromoCodeAsync(string code, CancellationToken ct) => db.PromoCodes.AsNoTracking().SingleOrDefaultAsync(p => p.Code == code, ct);

    public async Task LockPromoCodesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var ordered = ids.Distinct().Order().ToArray();
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM promo_codes WHERE id = ANY({ordered}) ORDER BY id FOR UPDATE", ct);
    }

    public Task<int> CountLiveRedemptionsAsync(Guid promoCodeId, Guid? userId, DateTimeOffset now, CancellationToken ct) =>
        db.PromoRedemptions.CountAsync(
            r => r.PromoCodeId == promoCodeId
                && (userId == null || r.UserId == userId)
                && (r.Status == PromoRedemptionStatus.Consumed || (r.Status == PromoRedemptionStatus.Reserved && r.ExpiresAt > now)),
            ct);

    public void AddRedemption(PromoRedemption redemption) => db.PromoRedemptions.Add(redemption);

    public Task<PromoRedemption?> FindLiveRedemptionAsync(Guid paymentId, CancellationToken ct) =>
        db.PromoRedemptions.SingleOrDefaultAsync(
            r => r.PaymentId == paymentId && (r.Status == PromoRedemptionStatus.Reserved || r.Status == PromoRedemptionStatus.Consumed),
            ct);

    public Task<PromoRedemption?> FindRedemptionForUpdateAsync(Guid id, CancellationToken ct) => ReloadedAsync(db.PromoRedemptions, id, ct);

    public Task<bool> WebhookEventExistsAsync(PaymentProvider provider, string eventId, CancellationToken ct) =>
        db.PaymentWebhookEvents.AnyAsync(e => e.Provider == provider && e.EventId == eventId, ct);

    public void AddWebhookEvent(PaymentWebhookEvent webhookEvent) => db.PaymentWebhookEvents.Add(webhookEvent);

    public async Task<IReadOnlyList<SettlementCandidate>> ListSettlementCandidatesAsync(int limit, CancellationToken ct) =>
        await (from p in db.Payments
               join a in db.Appointments on p.AppointmentId equals a.Id
               where p.Status == PaymentStatus.Authorized
                   && p.Provider != null
                   && p.CaptureFailedAt == null
                   && (p.CaptureRequestedAt != null
                       || a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.NoShow || a.Status == AppointmentStatus.Cancelled)
               orderby p.AuthorizedAt
               select new SettlementCandidate(p.Id, p.Provider!.Value, p.AuthorizationToken, p.AmountCents, p.Currency, a.Status, a.RefundPercent))
            .AsNoTracking()
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PendingRefund>> ListProcessingRefundsAsync(int limit, CancellationToken ct) =>
        await (from r in db.Refunds
               join p in db.Payments on r.PaymentId equals p.Id
               where r.Status == RefundStatus.Processing && p.Provider != null
               orderby r.CreatedAt
               select new PendingRefund(r.Id, p.Id, p.Provider!.Value, p.ProviderPaymentId, r.AmountCents, p.CapturedCents ?? 0, p.Currency))
            .AsNoTracking()
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ExpiredReservation>> ListExpiredReservationsAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        await (from r in db.PromoRedemptions
               join p in db.Payments on r.PaymentId equals p.Id
               where r.Status == PromoRedemptionStatus.Reserved
                   && r.ExpiresAt <= now
                   && !(p.Status == PaymentStatus.Pending && p.IntentCreatedAt != null)
               orderby r.ExpiresAt
               select new ExpiredReservation(r.Id, p.Id))
            .AsNoTracking()
            .Take(limit)
            .ToListAsync(ct);

    // A row read earlier in this scope would otherwise come back from the identity map with pre-lock values.
    private static async Task<T?> ReloadedAsync<T>(DbSet<T> set, Guid id, CancellationToken ct)
        where T : Domain.Common.Entity
    {
        if (set.Local.FirstOrDefault(e => e.Id == id) is { } tracked)
        {
            await set.Entry(tracked).ReloadAsync(ct);
            return tracked;
        }

        return await set.SingleOrDefaultAsync(e => e.Id == id, ct);
    }
}

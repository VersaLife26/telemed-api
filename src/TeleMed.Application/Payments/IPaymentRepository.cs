using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Payments;

// Lock order everywhere: calendar lock, then the payment row, then promo code rows in id order.
public interface IPaymentRepository
{
    void Add(Payment payment);
    Task<Payment?> FindAsync(Guid id, CancellationToken ct);
    Task<Payment?> FindByAppointmentAsync(Guid appointmentId, CancellationToken ct);
    // Takes a row lock (FOR UPDATE) for the rest of the transaction and returns the tracked, freshly read row.
    Task<Payment?> LockAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<Payment> Items, long Total)> ListForPatientAsync(Guid patientId, int skip, int take, CancellationToken ct);

    void AddRefund(Refund refund);
    Task<IReadOnlyList<Refund>> ListRefundsAsync(IReadOnlyCollection<Guid> paymentIds, CancellationToken ct);
    Task<Refund?> FindRefundForUpdateAsync(Guid id, CancellationToken ct);
    Task<long> SumOpenRefundsAsync(Guid paymentId, CancellationToken ct);

    Task<PromoCode?> FindPromoCodeAsync(string code, CancellationToken ct);
    Task LockPromoCodesAsync(IEnumerable<Guid> ids, CancellationToken ct);
    // Consumed plus unexpired reservations; userId null counts every user.
    Task<int> CountLiveRedemptionsAsync(Guid promoCodeId, Guid? userId, DateTimeOffset now, CancellationToken ct);
    void AddRedemption(PromoRedemption redemption);
    Task<PromoRedemption?> FindLiveRedemptionAsync(Guid paymentId, CancellationToken ct);
    Task<PromoRedemption?> FindRedemptionForUpdateAsync(Guid id, CancellationToken ct);

    Task<bool> WebhookEventExistsAsync(PaymentProvider provider, string eventId, CancellationToken ct);
    void AddWebhookEvent(PaymentWebhookEvent webhookEvent);

    Task<IReadOnlyList<SettlementCandidate>> ListSettlementCandidatesAsync(int limit, CancellationToken ct);
    Task<IReadOnlyList<PendingRefund>> ListProcessingRefundsAsync(int limit, CancellationToken ct);
    Task<IReadOnlyList<ExpiredReservation>> ListExpiredReservationsAsync(DateTimeOffset now, int limit, CancellationToken ct);
}

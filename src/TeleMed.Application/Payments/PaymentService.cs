using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Payments;

public sealed class PaymentService(
    ICurrentActor actor,
    IAppointmentRepository appointments,
    IPaymentRepository payments,
    IEnumerable<IPaymentProvider> providers,
    IPayHereWebhookVerifier payHere,
    IUserAccounts users,
    ICalendarLock calendar,
    IUnitOfWork unitOfWork,
    PaymentLifecycle lifecycle,
    IBillingSettingsRepository billingSettings,
    TimeProvider time)
{

    public async Task<OrderSummaryDto> GetSummaryAsync(Guid appointmentId, CancellationToken ct)
    {
        var (appointment, payment) = await RequireOwnAsync(appointmentId, ct);
        return await SummaryAsync(appointment, payment, ct);
    }

    public async Task<OrderSummaryDto> ApplyPromoAsync(Guid appointmentId, ApplyPromoRequest request, CancellationToken ct)
    {
        var (appointment, snapshot) = await RequireOwnAsync(appointmentId, ct);
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var payment = await payments.LockAsync(snapshot.Id, ct) ?? throw new NotFoundException("Payment not found.");
        EnsurePromoMutable(appointment, payment);

        var promo = await payments.FindPromoCodeAsync(PromoDiscount.Normalize(request.Code), ct)
            ?? throw new ConflictException("promo_unknown", "This promo code does not exist.");
        var existing = await payments.FindLiveRedemptionAsync(payment.Id, ct);
        await payments.LockPromoCodesAsync(existing is null ? [promo.Id] : [promo.Id, existing.PromoCodeId], ct);

        var now = time.GetUtcNow();
        if (existing?.PromoCodeId == promo.Id)
        {
            existing.ExpiresAt = now + PlatformPolicy.PromoReservationTtl;
        }
        else
        {
            if (existing is not null)
            {
                await lifecycle.ReleaseAsync(payment, existing, "replaced", restorePrice: true, ct);
                // The one-live-redemption-per-payment index must see the release before the new reservation.
                await unitOfWork.SaveChangesAsync(ct);
            }

            var discount = await ValidatePromoAsync(promo, payment, now, ct);
            var redemption = new PromoRedemption
            {
                PromoCodeId = promo.Id,
                UserId = payment.PatientId,
                PaymentId = payment.Id,
                AppointmentId = payment.AppointmentId,
                DiscountCents = discount,
                ExpiresAt = now + PlatformPolicy.PromoReservationTtl,
            };
            payments.AddRedemption(redemption);
            await lifecycle.SetPriceAsync(payment, discount, promo.Code, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await SummaryAsync(appointment, payment, ct);
    }

    public async Task<OrderSummaryDto> RemovePromoAsync(Guid appointmentId, CancellationToken ct)
    {
        var (appointment, snapshot) = await RequireOwnAsync(appointmentId, ct);
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var payment = await payments.LockAsync(snapshot.Id, ct) ?? throw new NotFoundException("Payment not found.");
        EnsurePromoMutable(appointment, payment);
        if (await payments.FindLiveRedemptionAsync(payment.Id, ct) is not { Status: PromoRedemptionStatus.Reserved } redemption)
        {
            throw new ConflictException("no_promo", "No promo code is applied to this payment.");
        }

        await lifecycle.ReleaseAsync(payment, redemption, "removed_by_patient", restorePrice: true, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await SummaryAsync(appointment, payment, ct);
    }

    public async Task<PaymentIntentDto> CreateIntentAsync(Guid appointmentId, CreateIntentRequest request, CancellationToken ct)
    {
        var (snapshot, _) = await RequireOwnAsync(appointmentId, ct);
        var provider = providers.FirstOrDefault(p => p.Provider == request.Provider && p.IsEnabled)
            ?? throw new BadRequestException("provider_unavailable", "This payment provider is not available.");
        var patient = await users.FindByIdAsync(snapshot.PatientId, ct);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await calendar.LockDoctorAsync(snapshot.DoctorId, ct);
        var appointment = await appointments.FindForUpdateAsync(appointmentId, ct) ?? throw new NotFoundException("Appointment not found.");
        var payment = await lifecycle.LockPaymentAsync(appointmentId, ct) ?? throw new NotFoundException("Payment not found.");
        var now = time.GetUtcNow();
        if (appointment.Status != AppointmentStatus.PendingPayment || appointment.PaymentDueAt <= now)
        {
            throw new ConflictException("payment_window_closed", "This booking is no longer awaiting payment.");
        }

        if (payment.Status is not (PaymentStatus.Pending or PaymentStatus.Failed))
        {
            throw new ConflictException("already_paid", "This booking has already been paid.");
        }

        payment.Provider = provider.Provider;
        payment.Status = PaymentStatus.Pending;
        payment.FailureReason = null;
        payment.AuthorizeOnly = await HoldCardAsync(appointment, payment.Currency, now, ct);
        payment.IntentCreatedAt = now;
        if (await payments.FindLiveRedemptionAsync(payment.Id, ct) is { Status: PromoRedemptionStatus.Reserved } redemption
            && redemption.ExpiresAt < appointment.PaymentDueAt)
        {
            redemption.ExpiresAt = appointment.PaymentDueAt.Value;
        }

        if (!provider.CanCharge(payment.Currency))
        {
            throw new BadRequestException("provider_unavailable", payment.Currency == ForeignPricing.Currency
                ? "International card payments are not available right now."
                : "This payment provider is not available.");
        }

        var intent = provider.CreateIntent(new PaymentIntentRequest(
            payment.Id,
            payment.AmountCents,
            payment.Currency,
            payment.AuthorizeOnly,
            $"Telemedicine consultation {appointment.Id}",
            patient?.Email,
            patient?.PhoneNumber));
        if (intent.Succeeded)
        {
            await lifecycle.ApplySuccessAsync(appointment, payment, payment.AuthorizeOnly, intent.Reference, intent.Reference, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new PaymentIntentDto(
            payment.Id,
            provider.Provider,
            payment.Status,
            payment.AuthorizeOnly,
            payment.AmountCents,
            payment.Currency,
            intent.Reference,
            intent is { ActionUrl: { } url, Fields: { } fields } ? new PayHereCheckoutDto(url, fields) : null);
    }

    public async Task<PagedResult<PaymentDto>> ListMineAsync(PaymentQuery query, CancellationToken ct)
    {
        var (items, total) = await payments.ListForPatientAsync(actor.RequireUserId(), query.Skip, query.PageSize, ct);
        var refunds = await payments.ListRefundsAsync(items.Select(p => p.Id).ToList(), ct);
        return new PagedResult<PaymentDto>(items.Select(p => p.ToDto(refunds)).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<PaymentDto> GetMineAsync(Guid id, CancellationToken ct)
    {
        var payment = await RequireOwnPaymentAsync(id, ct);
        return payment.ToDto(await payments.ListRefundsAsync([payment.Id], ct));
    }

    public async Task HandlePayHereWebhookAsync(IReadOnlyDictionary<string, string> fields, CancellationToken ct)
    {
        if (!payHere.IsEnabled)
        {
            throw new NotFoundException("PayHere is not enabled.");
        }

        var notification = payHere.Verify(fields);
        var snapshot = await payments.FindAsync(notification.PaymentId, ct) ?? throw new NotFoundException("Payment not found.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await calendar.LockDoctorAsync(snapshot.DoctorId, ct);
        if (await payments.WebhookEventExistsAsync(PaymentProvider.Payhere, notification.EventId, ct))
        {
            return;
        }

        var payment = await payments.LockAsync(snapshot.Id, ct) ?? throw new NotFoundException("Payment not found.");
        if (notification.AmountCents != payment.AmountCents || notification.Currency != payment.Currency)
        {
            throw new BadRequestException("amount_mismatch", "The notified amount does not match the payment.");
        }

        payments.AddWebhookEvent(new PaymentWebhookEvent
        {
            Provider = PaymentProvider.Payhere,
            EventId = notification.EventId,
            PaymentId = payment.Id,
            Outcome = notification.Outcome.ToString().ToLowerInvariant(),
            Payload = notification.PayloadJson,
        });
        var appointment = await appointments.FindForUpdateAsync(payment.AppointmentId, ct) ?? throw new NotFoundException("Appointment not found.");
        switch (notification.Outcome)
        {
            case PaymentNotificationOutcome.Authorized or PaymentNotificationOutcome.Succeeded:
                await lifecycle.ApplySuccessAsync(
                    appointment,
                    payment,
                    notification.Outcome == PaymentNotificationOutcome.Authorized,
                    notification.ProviderPaymentId,
                    notification.AuthorizationToken,
                    ct);
                break;
            case PaymentNotificationOutcome.Failed:
                await lifecycle.ApplyFailureAsync(payment, notification.FailureReason, ct);
                break;
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<PaymentDto> CompleteMockAsync(Guid paymentId, MockCompleteRequest request, CancellationToken ct)
    {
        if (!providers.Any(p => p.Provider == PaymentProvider.Mock && p.IsEnabled))
        {
            throw new NotFoundException("The mock payment provider is not enabled.");
        }

        var snapshot = await RequireOwnPaymentAsync(paymentId, ct);
        if (snapshot.Provider != PaymentProvider.Mock || snapshot.IntentCreatedAt is null)
        {
            throw new ConflictException("no_mock_intent", "This payment has no open mock intent.");
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await calendar.LockDoctorAsync(snapshot.DoctorId, ct);
        var payment = await payments.LockAsync(snapshot.Id, ct) ?? throw new NotFoundException("Payment not found.");
        var appointment = await appointments.FindForUpdateAsync(payment.AppointmentId, ct) ?? throw new NotFoundException("Appointment not found.");
        if (request.Outcome == MockOutcome.Succeed)
        {
            var reference = $"mock_{payment.Id:N}";
            await lifecycle.ApplySuccessAsync(appointment, payment, payment.AuthorizeOnly, reference, reference, ct);
        }
        else
        {
            await lifecycle.ApplyFailureAsync(payment, "mock_failure", ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return payment.ToDto(await payments.ListRefundsAsync([payment.Id], ct));
    }

    private async Task<long> ValidatePromoAsync(PromoCode promo, Payment payment, DateTimeOffset now, CancellationToken ct)
    {
        if (!promo.IsActive)
        {
            throw new ConflictException("promo_unknown", "This promo code does not exist.");
        }

        if (now < promo.ValidFrom || now >= promo.ValidUntil)
        {
            throw new ConflictException("promo_expired", "This promo code is not currently valid.");
        }

        var discount = PromoDiscount.Calculate(promo, payment.GrossCents);
        if (promo.Currency != payment.Currency || discount <= 0 || payment.GrossCents - discount < PlatformPolicy.MinChargeableCents)
        {
            throw new ConflictException("promo_not_applicable", "This promo code does not apply to this booking.");
        }

        if ((promo.MaxRedemptions is { } max && await payments.CountLiveRedemptionsAsync(promo.Id, null, now, ct) >= max)
            || await payments.CountLiveRedemptionsAsync(promo.Id, payment.PatientId, now, ct) >= promo.MaxPerUser)
        {
            throw new ConflictException("promo_exhausted", "This promo code has no redemptions left.");
        }

        return discount;
    }

    private static void EnsurePromoMutable(Appointment appointment, Payment payment)
    {
        if (appointment.Status != AppointmentStatus.PendingPayment || payment.Status != PaymentStatus.Pending || payment.IntentCreatedAt is not null)
        {
            throw new ConflictException("promo_locked", "Promo codes cannot be changed once payment has started.");
        }
    }

    private async Task<OrderSummaryDto> SummaryAsync(Appointment appointment, Payment payment, CancellationToken ct)
    {
        var redemption = await payments.FindLiveRedemptionAsync(payment.Id, ct);
        return new OrderSummaryDto(
            payment.Id,
            payment.AppointmentId,
            payment.Status,
            payment.GrossCents,
            payment.DiscountCents,
            payment.AmountCents,
            payment.Currency,
            payment.PromoCode,
            redemption is { Status: PromoRedemptionStatus.Reserved } ? redemption.ExpiresAt : null,
            payment.Provider,
            payment.IntentCreatedAt is not null,
            appointment.PaymentDueAt,
            providers.Where(p => p.IsEnabled).Select(p => p.Provider).ToList(),
            await HoldCardAsync(appointment, payment.Currency, time.GetUtcNow(), ct));
    }

    private async Task<bool> HoldCardAsync(Appointment appointment, string currency, DateTimeOffset now, CancellationToken ct)
    {
        var settings = await billingSettings.GetAsync(ct);
        return CardHold.Applies(
            currency,
            appointment.StartAt - now <= PlatformPolicy.MaxCardHoldLead,
            settings.HoldLkrWithinSixDays,
            settings.HoldUsdWithinSixDays);
    }

    private async Task<(Appointment Appointment, Payment Payment)> RequireOwnAsync(Guid appointmentId, CancellationToken ct)
    {
        var userId = actor.RequireUserId();
        var appointment = await appointments.FindAsync(appointmentId, ct);
        if (appointment is null || appointment.PatientId != userId)
        {
            throw new NotFoundException("Appointment not found.");
        }

        var payment = await payments.FindByAppointmentAsync(appointmentId, ct) ?? throw new NotFoundException("This appointment has no payment.");
        return (appointment, payment);
    }

    private async Task<Payment> RequireOwnPaymentAsync(Guid id, CancellationToken ct)
    {
        var payment = await payments.FindAsync(id, ct);
        return payment is not null && payment.PatientId == actor.RequireUserId() ? payment : throw new NotFoundException("Payment not found.");
    }
}

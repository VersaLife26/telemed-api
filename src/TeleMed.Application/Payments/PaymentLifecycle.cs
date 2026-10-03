using TeleMed.Application.Admin.Finance;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Application.Notifications;
using TeleMed.Application.Payouts;
using TeleMed.Application.Reschedules;
using TeleMed.Application.Scheduling;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Payments;

// State changes shared by booking, payment and job flows. Callers hold the transaction, the calendar lock
// and (via LockPaymentAsync) the payment row lock, and load the appointment only after taking them.
public sealed class PaymentLifecycle(
    IPaymentRepository payments,
    IAppointmentRepository appointments,
    IDoctorRepository doctors,
    ISchedulingRepository scheduling,
    IRescheduleRepository reschedules,
    IPayoutRepository payouts,
    ICommissionPolicy commission,
    AppointmentNotifier notifier,
    TimeProvider time)
{
    public async Task<Payment?> LockPaymentAsync(Guid appointmentId, CancellationToken ct) =>
        await payments.FindByAppointmentAsync(appointmentId, ct) is { } row ? await payments.LockAsync(row.Id, ct) : null;

    public Task CancelAsync(Appointment appointment, Payment? payment, CancellationActor by, Guid? byId, string? reason, CancellationToken ct) =>
        CancelAsync(appointment, payment, by, byId, reason, percentOverride: null, CancellationPolicy.ReasonFor(by), ct);

    // The doctor asked to move the appointment, so the patient saying no (or never answering) costs them nothing.
    public Task CancelWithFullRefundAsync(Appointment appointment, Payment? payment, CancellationActor by, Guid? byId, string reason, CancellationToken ct) =>
        CancelAsync(appointment, payment, by, byId, reason, PlatformPolicy.FullRefundPercent, RefundReason.RescheduleDeclined, ct);

    public Task CancelForPatientSuspensionAsync(Appointment appointment, Payment? payment, Guid adminId, CancellationToken ct) =>
        CancelAsync(appointment, payment, CancellationActor.Admin, adminId, PlatformPolicy.PatientSuspendedReason, PlatformPolicy.FullRefundPercent, RefundReason.AdminCancellation, ct);

    public Task CancelForDoctorNoShowAsync(Appointment appointment, Payment? payment, CancellationToken ct) =>
        CancelAsync(appointment, payment, CancellationActor.System, null, PlatformPolicy.DoctorNoShowReason, PlatformPolicy.FullRefundPercent, RefundReason.DoctorNoShow, ct);

    // An authorized payment on a completed or no-show appointment is captured by PaymentSettlementJob.
    public void Complete(Appointment appointment)
    {
        EnsureTransition(appointment, AppointmentStatus.Completed);
        appointment.Status = AppointmentStatus.Completed;
        appointment.CompletedAt = time.GetUtcNow();
    }

    public async Task MarkPatientNoShowAsync(Appointment appointment, Payment? payment, CancellationToken ct)
    {
        EnsureTransition(appointment, AppointmentStatus.NoShow);
        appointment.Status = AppointmentStatus.NoShow;
        appointment.NoShowAt = time.GetUtcNow();
        if (payment is null)
        {
            appointment.RefundPercent = null;
            return;
        }

        appointment.RefundPercent = CancellationPolicy.PatientNoShowRefundPercent(payment.CurrentSplit());
        if (payment.Status is PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded)
        {
            await StagePatientNoShowRefundAsync(payment, appointment.RefundPercent.Value, ct);
        }
    }

    // Called after an authorized hold is captured for a no-show appointment.
    public Task StagePatientNoShowRefundIfNeededAsync(Payment payment, Appointment appointment, CancellationToken ct) =>
        appointment.Status == AppointmentStatus.NoShow
            ? StagePatientNoShowRefundAsync(
                payment,
                appointment.RefundPercent ?? CancellationPolicy.PatientNoShowRefundPercent(payment.CurrentSplit()),
                ct)
            : Task.CompletedTask;

    private async Task StagePatientNoShowRefundAsync(Payment payment, int refundPercent, CancellationToken ct)
    {
        var captured = payment.CapturedCents ?? 0;
        if (captured <= 0)
        {
            return;
        }

        var payoutRemaining = payment.PayoutCents - payment.RefundedPayoutCents;
        if (payoutRemaining <= 0)
        {
            return;
        }

        var reserved = payment.RefundedCents + await payments.SumOpenRefundsAsync(payment.Id, ct);
        var amount = Math.Min(payoutRemaining, captured - reserved);
        if (amount <= 0)
        {
            return;
        }

        payments.AddRefund(new Refund
        {
            PaymentId = payment.Id,
            AmountCents = amount,
            CommissionCents = 0,
            ProviderFeeCents = 0,
            PayoutCents = amount,
            Percent = refundPercent,
            Reason = RefundReason.PatientNoShow,
        });
    }

    private static void EnsureTransition(Appointment appointment, AppointmentStatus to)
    {
        if (!AppointmentTransitions.IsAllowed(appointment.Status, to))
        {
            throw new ConflictException("invalid_transition", "This appointment can no longer change to that status.");
        }
    }

    private async Task CancelAsync(
        Appointment appointment, Payment? payment, CancellationActor by, Guid? byId, string? reason, int? percentOverride, RefundReason refundReason, CancellationToken ct)
    {
        if (!AppointmentTransitions.IsAllowed(appointment.Status, AppointmentStatus.Cancelled))
        {
            throw new ConflictException("invalid_transition", "This appointment can no longer be cancelled.");
        }

        var now = time.GetUtcNow();
        var previousStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAt = now;
        appointment.CancelledBy = by;
        appointment.CancelledById = byId;
        appointment.CancellationReason = reason;
        if (await reschedules.FindPendingForAppointmentAsync(appointment.Id, ct) is { Status: RescheduleStatus.Pending } pending)
        {
            pending.Decide(RescheduleStatus.Expired, by, byId, now);
        }

        await notifier.CancelledAsync(appointment, previousStatus, by, ct);
        if (payment is null)
        {
            return;
        }

        var percent = percentOverride ?? CancellationPolicy.RefundPercent(by, now, appointment.StartAt);
        switch (payment.Status)
        {
            case PaymentStatus.Pending or PaymentStatus.Failed:
                if (payment.Status == PaymentStatus.Pending)
                {
                    payment.Status = PaymentStatus.Failed;
                    payment.FailedAt = now;
                    payment.FailureReason = by == CancellationActor.System && reason is not null ? reason : "appointment_cancelled";
                }

                await ReleasePromoAsync(payment, "appointment_cancelled", restorePrice: false, ct);
                break;
            case PaymentStatus.Authorized:
                appointment.RefundPercent = percent;
                break;
            case PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded:
                appointment.RefundPercent = percent;
                await StageRefundAsync(payment, percent, refundReason, ct);
                break;
        }
    }

    public async Task ApplySuccessAsync(Appointment appointment, Payment payment, bool authorized, string? providerPaymentId, string? authorizationToken, CancellationToken ct)
    {
        if (payment.Status is not (PaymentStatus.Pending or PaymentStatus.Failed))
        {
            return;
        }

        var now = time.GetUtcNow();
        payment.ProviderPaymentId = providerPaymentId ?? payment.ProviderPaymentId;
        payment.FailureReason = null;
        if (authorized)
        {
            payment.Status = PaymentStatus.Authorized;
            payment.AuthorizedAt = now;
            payment.AuthorizationToken = authorizationToken;
        }
        else
        {
            payment.Status = PaymentStatus.Succeeded;
            payment.SucceededAt = now;
            payment.CapturedCents = payment.AmountCents;
        }

        if (await payments.FindLiveRedemptionAsync(payment.Id, ct) is { Status: PromoRedemptionStatus.Reserved } redemption)
        {
            redemption.Status = PromoRedemptionStatus.Consumed;
            redemption.ConsumedAt = now;
        }

        if (appointment.Status == AppointmentStatus.PendingPayment)
        {
            appointment.Status = AppointmentStatus.Confirmed;
            appointment.ConfirmedAt = now;
            await notifier.ConfirmedAsync(appointment, payment, ct);
        }
        else if (appointment.Status == AppointmentStatus.Cancelled)
        {
            await SettleLatePaymentAsync(appointment, payment, ct);
        }
    }

    // Called after an accepted reschedule has moved the appointment. A PayHere hold lapses 7 days after authorisation,
    // so a start beyond that is charged now rather than at the visit.
    public async Task ApplyRescheduleAsync(Appointment appointment, CancellationToken ct)
    {
        if (await LockPaymentAsync(appointment.Id, ct) is { Status: PaymentStatus.Authorized, AuthorizedAt: { } authorizedAt } payment
            && appointment.StartAt - authorizedAt > PlatformPolicy.MaxCardHoldLead)
        {
            payment.CaptureRequestedAt ??= time.GetUtcNow();
        }
    }

    public async Task ApplyFailureAsync(Payment payment, string? reason, CancellationToken ct)
    {
        if (payment.Status != PaymentStatus.Pending)
        {
            return;
        }

        payment.Status = PaymentStatus.Failed;
        payment.FailedAt = time.GetUtcNow();
        payment.FailureReason = reason ?? "payment_failed";
        payment.IntentCreatedAt = null;
        await ReleasePromoAsync(payment, "payment_failed", restorePrice: true, ct);
        await notifier.PaymentFailedAsync(payment, ct);
    }

    public async Task ReleasePromoAsync(Payment payment, string reason, bool restorePrice, CancellationToken ct)
    {
        if (await payments.FindLiveRedemptionAsync(payment.Id, ct) is { Status: PromoRedemptionStatus.Reserved } redemption)
        {
            await ReleaseAsync(payment, redemption, reason, restorePrice, ct);
        }
    }

    public async Task ReleaseAsync(Payment payment, PromoRedemption redemption, string reason, bool restorePrice, CancellationToken ct)
    {
        redemption.Status = PromoRedemptionStatus.Released;
        redemption.ReleasedAt = time.GetUtcNow();
        redemption.ReleaseReason = reason;
        if (restorePrice)
        {
            await SetPriceAsync(payment, discountCents: 0, promoCode: null, ct);
        }
    }

    public async Task SetPriceAsync(Payment payment, long discountCents, string? promoCode, CancellationToken ct)
    {
        payment.DiscountCents = discountCents;
        payment.AmountCents = payment.GrossCents - discountCents;
        payment.PromoCode = promoCode;
        payment.ApplySplit(CommissionCalculator.Split(payment.AmountCents, await commission.GetEffectiveBpsAsync(payment.DoctorId, ct)));
    }

    public async Task StageRefundAsync(Payment payment, int percent, RefundReason reason, CancellationToken ct)
    {
        var captured = payment.CapturedCents ?? 0;
        var refundable = captured - payment.RefundedCents - await payments.SumOpenRefundsAsync(payment.Id, ct);
        var amount = Math.Min(CommissionCalculator.PercentOf(captured, percent), refundable);
        if (amount <= 0)
        {
            return;
        }

        var split = CommissionCalculator.Prorate(payment.CurrentSplit(), amount);
        payments.AddRefund(new Refund
        {
            PaymentId = payment.Id,
            AmountCents = amount,
            CommissionCents = split.CommissionCents,
            ProviderFeeCents = split.ProviderFeeCents,
            PayoutCents = split.PayoutCents,
            Percent = percent,
            Reason = reason,
        });
    }

    // An admin-initiated refund waits in `requested` for finance approval; the amount is held against the refundable balance meanwhile.
    public async Task<Refund> StageRequestedRefundAsync(Payment payment, long amountCents, RefundReason reason, string note, Guid adminId, Guid? disputeId, CancellationToken ct)
    {
        if (payment.Status is not (PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded) || payment.CapturedCents is not { } captured)
        {
            throw new ConflictException("payment_not_refundable", "Only a captured payment can be refunded.");
        }

        var refundable = captured - payment.RefundedCents - await payments.SumOpenRefundsAsync(payment.Id, ct);
        if (amountCents > refundable)
        {
            throw new ConflictException("refund_exceeds_refundable", "The refund is larger than what is left to refund on this payment.")
            {
                Extensions = { ["refundableCents"] = Math.Max(refundable, 0) },
            };
        }

        var split = CommissionCalculator.Prorate(payment.CurrentSplit(), amountCents);
        var refund = new Refund
        {
            PaymentId = payment.Id,
            AmountCents = amountCents,
            CommissionCents = split.CommissionCents,
            ProviderFeeCents = split.ProviderFeeCents,
            PayoutCents = split.PayoutCents,
            Percent = (int)Math.Clamp(CommissionCalculator.RoundHalfUp(amountCents * 100, captured), 1, 100),
            Reason = reason,
            Status = RefundStatus.Requested,
            Note = note,
            RequestedByAdminId = adminId,
            DisputeId = disputeId,
        };
        payments.AddRefund(refund);
        return refund;
    }

    // Once the payment has gone into a payout the doctor's share was already sent (or is about to be), so it is clawed back from the next payout.
    public void ApplyRefundSucceeded(Payment payment, Refund refund, string? reference)
    {
        if (payment.PayoutId is not null && refund.PayoutCents > 0)
        {
            payouts.AddAdjustment(new PayoutAdjustment
            {
                DoctorId = payment.DoctorId,
                RefundId = refund.Id,
                PaymentId = payment.Id,
                AmountCents = -refund.PayoutCents,
                Currency = payment.Currency,
            });
        }

        refund.Status = RefundStatus.Succeeded;
        refund.ProviderRefundId = reference;
        payment.RefundedCents += refund.AmountCents;
        payment.RefundedCommissionCents += refund.CommissionCents;
        payment.RefundedProviderFeeCents += refund.ProviderFeeCents;
        payment.RefundedPayoutCents += refund.PayoutCents;
        payment.Status = payment.RefundedCents >= payment.CapturedCents ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
    }

    private async Task SettleLatePaymentAsync(Appointment appointment, Payment payment, CancellationToken ct)
    {
        var expiredByUs = appointment.CancelledBy == CancellationActor.System && appointment.CancellationReason == PlatformPolicy.PaymentTimeoutReason;
        if (expiredByUs && await IsStillFreeAsync(appointment, ct))
        {
            // Cancelled is otherwise terminal; the only thing that stopped this booking was our own payment timer.
            appointment.Status = AppointmentStatus.Confirmed;
            appointment.ConfirmedAt = time.GetUtcNow();
            appointment.CancelledAt = null;
            appointment.CancelledBy = null;
            appointment.CancelledById = null;
            appointment.CancellationReason = null;
            appointment.RefundPercent = null;
            await notifier.ConfirmedAsync(appointment, payment, ct);
            return;
        }

        appointment.RefundPercent = PlatformPolicy.FullRefundPercent;
        if (payment.Status == PaymentStatus.Succeeded)
        {
            await StageRefundAsync(payment, PlatformPolicy.FullRefundPercent, RefundReason.LatePayment, ct);
        }

        await notifier.PaymentRefundedAsync(appointment, payment, ct);
    }

    private async Task<bool> IsStillFreeAsync(Appointment appointment, CancellationToken ct)
    {
        if (appointment.StartAt <= time.GetUtcNow()
            || await doctors.FindAsync(appointment.DoctorId, ct) is not { Status: DoctorStatus.Active } doctor)
        {
            return false;
        }

        var date = SlotPlanner.LocalDate(appointment.StartAt, IanaTimeZone.Find(doctor.TimeZone));
        return (await scheduling.ListHolidaysAsync(doctor.Id, date, date, ct)).Count == 0
            && (await scheduling.ListBusyAsync(doctor.Id, appointment.StartAt, appointment.EndAt, null, ct)).Count == 0
            && !await appointments.PatientHasLiveOverlapAsync(appointment.PatientId, appointment.StartAt, appointment.EndAt, null, ct);
    }
}

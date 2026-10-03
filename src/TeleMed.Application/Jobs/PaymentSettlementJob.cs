using TeleMed.Application.Abstractions;
using TeleMed.Application.Admin.Finance;
using TeleMed.Application.Admin.Notifications;
using TeleMed.Application.Appointments;
using TeleMed.Application.Notifications.Templates;
using TeleMed.Application.Payments;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Jobs;

// Provider calls happen outside any transaction; the outcome is then applied under the payment row lock,
// re-checking the state in case something else settled the payment in the meantime.
public sealed class PaymentSettlementJob(
    IPaymentRepository payments,
    IAppointmentRepository appointments,
    IEnumerable<IPaymentProvider> providers,
    IUnitOfWork unitOfWork,
    PaymentLifecycle lifecycle,
    ICommissionPolicy commission,
    AdminNotificationService adminInbox,
    TimeProvider time) : IBackgroundJob
{
    private const int BatchSize = 100;

    public async Task RunAsync(CancellationToken ct)
    {
        await SettleAuthorizedAsync(ct);
        await ProcessRefundsAsync(ct);
        await ReleaseExpiredPromosAsync(ct);
    }

    private async Task SettleAuthorizedAsync(CancellationToken ct)
    {
        foreach (var candidate in await payments.ListSettlementCandidatesAsync(BatchSize, ct))
        {
            if (Enabled(candidate.Provider) is not { } provider)
            {
                continue;
            }

            var keepPercent = candidate.AppointmentStatus == AppointmentStatus.Cancelled
                ? PlatformPolicy.FullRefundPercent - (candidate.RefundPercent ?? PlatformPolicy.FullRefundPercent)
                : PlatformPolicy.FullRefundPercent;
            var capture = CommissionCalculator.PercentOf(candidate.AmountCents, keepPercent);
            var result = capture == 0
                ? await provider.VoidAsync(new ProviderVoidRequest(candidate.PaymentId, candidate.AuthorizationToken), ct)
                : await provider.CaptureAsync(new ProviderCaptureRequest(candidate.PaymentId, candidate.AuthorizationToken, capture, candidate.Currency), ct);
            if (result.Status == ProviderResultStatus.Unavailable)
            {
                continue;
            }

            await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
            var payment = await payments.LockAsync(candidate.PaymentId, ct);
            if (payment is not { Status: PaymentStatus.Authorized })
            {
                continue;
            }

            var now = time.GetUtcNow();
            if (result.Status is ProviderResultStatus.Rejected or ProviderResultStatus.NotSupported)
            {
                payment.FailureReason = result.Message;
                payment.CaptureAttempts++;
                if (payment.CaptureAttempts >= PlatformPolicy.MaxCaptureAttempts)
                {
                    payment.CaptureFailedAt = now;
                    adminInbox.Add(
                        AdminNotificationKind.PaymentCaptureFailed,
                        "Payment capture failed",
                        $"Settling {NotificationFormat.Money(payment.AmountCents, payment.Currency)} via {payment.Provider} failed {payment.CaptureAttempts} times: {result.Message}",
                        $"/appointments/{payment.AppointmentId}",
                        payment.Id);
                }
            }
            else if (capture == 0)
            {
                payment.Status = PaymentStatus.Voided;
                payment.VoidedAt = now;
            }
            else
            {
                payment.Status = PaymentStatus.Succeeded;
                payment.SucceededAt = now;
                payment.CapturedCents = capture;
                payment.ProviderPaymentId = result.Reference ?? payment.ProviderPaymentId;
                payment.FailureReason = null;
                payment.ApplySplit(CommissionCalculator.Split(capture, await commission.GetEffectiveBpsAsync(payment.DoctorId, ct)));
                if (await appointments.FindForUpdateAsync(payment.AppointmentId, ct) is { Status: AppointmentStatus.NoShow } appointment)
                {
                    await lifecycle.StagePatientNoShowRefundIfNeededAsync(payment, appointment, ct);
                }
            }

            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }

    private async Task ProcessRefundsAsync(CancellationToken ct)
    {
        foreach (var pending in await payments.ListProcessingRefundsAsync(BatchSize, ct))
        {
            if (Enabled(pending.Provider) is not { } provider)
            {
                continue;
            }

            var result = await provider.RefundAsync(
                new ProviderRefundRequest(pending.PaymentId, pending.RefundId, pending.ProviderPaymentId, pending.AmountCents, pending.CapturedCents, pending.Currency),
                ct);
            if (result.Status == ProviderResultStatus.Unavailable)
            {
                continue;
            }

            await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
            var payment = await payments.LockAsync(pending.PaymentId, ct);
            var refund = await payments.FindRefundForUpdateAsync(pending.RefundId, ct);
            if (payment is null || refund is not { Status: RefundStatus.Processing })
            {
                continue;
            }

            refund.ProcessedAt = time.GetUtcNow();
            if (result.Status == ProviderResultStatus.NotSupported)
            {
                refund.Status = RefundStatus.ManualRequired;
                refund.FailureReason = result.Message;
                adminInbox.Add(
                    AdminNotificationKind.RefundManualRequired,
                    "Refund needs manual processing",
                    $"A refund of {NotificationFormat.Money(refund.AmountCents, payment.Currency)} cannot be sent through {payment.Provider}: {result.Message}",
                    $"/appointments/{payment.AppointmentId}",
                    refund.Id);
            }
            else if (result.Status == ProviderResultStatus.Rejected)
            {
                refund.Status = RefundStatus.Failed;
                refund.FailureReason = result.Message;
            }
            else
            {
                lifecycle.ApplyRefundSucceeded(payment, refund, result.Reference);
            }

            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }

    private async Task ReleaseExpiredPromosAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var expired in await payments.ListExpiredReservationsAsync(now, BatchSize, ct))
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
            var payment = await payments.LockAsync(expired.PaymentId, ct);
            var redemption = await payments.FindRedemptionForUpdateAsync(expired.RedemptionId, ct);
            if (payment is null
                || redemption is not { Status: PromoRedemptionStatus.Reserved }
                || redemption.ExpiresAt > now
                || (payment.Status == PaymentStatus.Pending && payment.IntentCreatedAt is not null))
            {
                continue;
            }

            await lifecycle.ReleaseAsync(payment, redemption, "expired", restorePrice: payment.Status == PaymentStatus.Pending, ct);
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }

    private IPaymentProvider? Enabled(PaymentProvider provider) => providers.FirstOrDefault(p => p.Provider == provider && p.IsEnabled);
}

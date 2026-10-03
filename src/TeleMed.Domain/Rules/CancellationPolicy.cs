using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Rules;

public static class CancellationPolicy
{
    /// <summary>
    /// Share of the captured fee returned to the patient when the doctor marks a no-show.
    /// Commission and provider fee stay with the platform; the doctor's share is refunded.
    /// </summary>
    public static int PatientNoShowRefundPercent(long amountCents) =>
        PatientNoShowRefundPercent(CommissionCalculator.Split(amountCents));

    public static int PatientNoShowRefundPercent(PaymentSplit split)
    {
        if (split.AmountCents <= 0)
        {
            return 0;
        }

        return (int)Math.Clamp(CommissionCalculator.RoundHalfUp(split.PayoutCents * 100, split.AmountCents), 0, 100);
    }

    // Exactly two hours' notice counts as early: the patient gets the boundary.
    public static int RefundPercent(CancellationActor actor, DateTimeOffset cancelledAt, DateTimeOffset startAt) =>
        actor == CancellationActor.Patient && startAt - cancelledAt < PlatformPolicy.FreeCancellationWindow
            ? PlatformPolicy.LateCancellationRefundPercent
            : PlatformPolicy.FullRefundPercent;

    public static RefundReason ReasonFor(CancellationActor actor) => actor switch
    {
        CancellationActor.Patient => RefundReason.PatientCancellation,
        CancellationActor.Doctor => RefundReason.DoctorCancellation,
        CancellationActor.Admin => RefundReason.AdminCancellation,
        _ => RefundReason.SystemCancellation,
    };
}

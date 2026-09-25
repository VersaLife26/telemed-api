using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Rules;

public static class CancellationPolicy
{
    public const int NoShowRefundPercent = 0;

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

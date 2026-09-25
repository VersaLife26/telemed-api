using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Payments;
using TeleMed.Application.Reschedules;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Jobs;

// An unanswered proposal is a decline once the original time passes: the doctor could not make it, so the patient gets everything back.
public sealed class ExpireRescheduleRequestsJob(
    IRescheduleRepository reschedules,
    IAppointmentRepository appointments,
    ICalendarLock calendar,
    IUnitOfWork unitOfWork,
    PaymentLifecycle lifecycle,
    TimeProvider time) : IBackgroundJob
{
    private const int BatchSize = 100;

    public async Task RunAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var expired in await reschedules.ListExpiredPendingAsync(now, BatchSize, ct))
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
            await calendar.LockDoctorAsync(expired.DoctorId, ct);
            var request = await reschedules.FindForUpdateAsync(expired.RequestId, ct);
            if (request is not { Status: RescheduleStatus.Pending } || request.OriginalStartAt > now)
            {
                continue;
            }

            request.Decide(RescheduleStatus.Expired, CancellationActor.System, null, now);
            var appointment = await appointments.FindForUpdateAsync(request.AppointmentId, ct);
            if (appointment is not null && AppointmentTransitions.IsAllowed(appointment.Status, AppointmentStatus.Cancelled))
            {
                var payment = await lifecycle.LockPaymentAsync(appointment.Id, ct);
                await lifecycle.CancelWithFullRefundAsync(appointment, payment, CancellationActor.System, null, PlatformPolicy.RescheduleExpiredReason, ct);
            }

            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }
}

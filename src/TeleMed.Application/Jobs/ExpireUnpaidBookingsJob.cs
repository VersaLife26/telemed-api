using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Payments;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Jobs;

public sealed class ExpireUnpaidBookingsJob(
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
        foreach (var expired in await appointments.ListExpiredUnpaidAsync(now, BatchSize, ct))
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
            await calendar.LockDoctorAsync(expired.DoctorId, ct);
            var appointment = await appointments.FindForUpdateAsync(expired.AppointmentId, ct);
            if (appointment is not { Status: AppointmentStatus.PendingPayment } || appointment.PaymentDueAt > now)
            {
                continue;
            }

            var payment = await lifecycle.LockPaymentAsync(appointment.Id, ct);
            await lifecycle.CancelAsync(appointment, payment, CancellationActor.System, null, PlatformPolicy.PaymentTimeoutReason, ct);
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }
}

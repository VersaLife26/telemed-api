using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Payments;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Jobs;

public sealed class AutoCompleteAppointmentsJob(
    IAppointmentRepository appointments,
    IUnitOfWork unitOfWork,
    PaymentLifecycle lifecycle,
    TimeProvider time) : IBackgroundJob
{
    private const int BatchSize = 100;

    public async Task RunAsync(CancellationToken ct)
    {
        foreach (var id in await appointments.ListConfirmedEndedBeforeAsync(time.GetUtcNow() - PlatformPolicy.AutoCompleteAfter, BatchSize, ct))
        {
            if (await appointments.FindForUpdateAsync(id, ct) is { Status: AppointmentStatus.Confirmed } appointment)
            {
                lifecycle.Complete(appointment);
                await unitOfWork.SaveChangesAsync(ct);
            }
        }
    }
}

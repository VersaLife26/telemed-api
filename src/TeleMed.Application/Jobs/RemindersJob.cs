using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Notifications;
using TeleMed.Application.Notifications.Templates;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Jobs;

// A reminder is due once the appointment is inside its lead time, provided the booking (or its last confirmation)
// predates that lead time. The dedupe key includes the start time, so a rescheduled appointment is reminded afresh.
public sealed class RemindersJob(
    IAppointmentRepository appointments,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IBackgroundJob
{
    private static readonly TimeSpan DayLead = PlatformPolicy.ReminderLeadTimes.Max();
    private static readonly TimeSpan HourLead = PlatformPolicy.ReminderLeadTimes.Min();

    public async Task RunAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var due in await appointments.ListReminderCandidatesAsync(now, now + DayLead, ct))
        {
            var (model, suffix, lead) = due.StartAt <= now + HourLead
                ? (ReminderModel.HourBefore(due.DoctorName, due.StartAt), "r1", HourLead)
                : (ReminderModel.DayBefore(due.DoctorName, due.StartAt), "r24", DayLead);
            if (due.ConfirmedAt <= due.StartAt - lead)
            {
                await notifications.EnqueueAsync(due.PatientId, model, $"appt:{due.AppointmentId}:{due.StartAt:O}:{suffix}", ct);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}

using TeleMed.Application.Abstractions;
using TeleMed.Application.Admin.Notifications;
using TeleMed.Application.Appointments;
using TeleMed.Application.Consultations;
using TeleMed.Application.Doctors;
using TeleMed.Application.Notifications;
using TeleMed.Application.Notifications.Templates;
using TeleMed.Application.Payments;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Jobs;

// Closes out appointments nobody (or only one side) attended, warns the next patient when a call overruns,
// and ends calls whose peers have been gone for a long time.
public sealed class ConsultationSweepJob(
    IAppointmentRepository appointments,
    IConsultationRepository consultations,
    IDoctorRepository doctors,
    IConsultationPresence presence,
    IConsultationRealtime realtime,
    ICalendarLock calendar,
    IUnitOfWork unitOfWork,
    PaymentLifecycle lifecycle,
    AppointmentNotifier notifier,
    AdminNotificationService adminInbox,
    TimeProvider time) : IBackgroundJob
{
    private const int BatchSize = 100;

    public async Task RunAsync(CancellationToken ct)
    {
        await CloseUnattendedAsync(ct);
        await NotifyRunningLateAsync(ct);
        await EndIdleAsync(ct);
        await AbandonStuckAsync(ct);
    }

    // A doctor who never joined is on us: the patient gets everything back. That includes the case where neither side joined,
    // since the patient cannot start a consultation the doctor never opened.
    private async Task CloseUnattendedAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var unattended in await consultations.ListUnattendedEndedAsync(now, BatchSize, ct))
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
            await calendar.LockDoctorAsync(unattended.DoctorId, ct);
            var appointment = await appointments.FindForUpdateAsync(unattended.AppointmentId, ct);
            if (appointment is not { Status: AppointmentStatus.Confirmed } || appointment.EndAt > now)
            {
                continue;
            }

            var consultation = await consultations.FindForUpdateByAppointmentAsync(appointment.Id, ct);
            var doctorJoined = consultation?.DoctorJoinedAt is not null;
            if (doctorJoined && consultation!.PatientJoinedAt is not null)
            {
                continue;
            }

            string reason;
            if (doctorJoined)
            {
                var payment = await lifecycle.LockPaymentAsync(appointment.Id, ct);
                await lifecycle.MarkPatientNoShowAsync(appointment, payment, ct);
                reason = PlatformPolicy.PatientNoShowReason;
            }
            else
            {
                var payment = await lifecycle.LockPaymentAsync(appointment.Id, ct);
                await lifecycle.CancelForDoctorNoShowAsync(appointment, payment, ct);
                reason = PlatformPolicy.DoctorNoShowReason;
                if (!appointment.IsTest)
                {
                    var doctor = await doctors.FindAsync(appointment.DoctorId, ct);
                    adminInbox.Add(
                        AdminNotificationKind.DoctorNoShow,
                        "Doctor did not join a consultation",
                        $"Dr. {doctor?.DisplayName} did not join the appointment at {NotificationFormat.DateTime(appointment.StartAt)}. It was cancelled with a full refund.",
                        $"/appointments/{appointment.Id}",
                        appointment.Id);
                }
            }

            var closed = consultation is not null && !ConsultationTransitions.IsTerminal(consultation.Status);
            if (closed)
            {
                End(consultation!, ConsultationStatus.Abandoned, reason, now);
            }

            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            if (closed)
            {
                await PublishClosedAsync(consultation!, reason, ct);
            }
        }
    }

    private async Task NotifyRunningLateAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var overrun in await consultations.ListOverrunActiveAsync(now, BatchSize, ct))
        {
            var next = await appointments.FindNextConfirmedAsync(
                overrun.DoctorId, overrun.AppointmentId, overrun.StartAt, now + PlatformPolicy.RunningLateLookahead, ct);
            if (next is null)
            {
                continue;
            }

            var consultation = await consultations.FindForUpdateAsync(overrun.ConsultationId, ct);
            if (consultation is not { Status: ConsultationStatus.Active, RunningLateNotifiedAt: null })
            {
                continue;
            }

            consultation.RunningLateNotifiedAt = now;
            await notifier.DoctorRunningLateAsync(next, consultation.Id, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    private async Task EndIdleAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var idle in await consultations.ListIdleActiveAsync(now - PlatformPolicy.StaleConsultationAfter, BatchSize, ct))
        {
            if (presence.IsOccupied(idle.ConsultationId))
            {
                continue;
            }

            var consultation = await consultations.FindForUpdateAsync(idle.ConsultationId, ct);
            if (consultation is not { Status: ConsultationStatus.Active })
            {
                continue;
            }

            End(consultation, ConsultationStatus.Ended, PlatformPolicy.ConsultationStaleReason, now);
            if (await appointments.FindForUpdateAsync(idle.AppointmentId, ct) is { Status: AppointmentStatus.Confirmed } appointment)
            {
                lifecycle.Complete(appointment);
            }

            await unitOfWork.SaveChangesAsync(ct);
            await PublishClosedAsync(consultation, PlatformPolicy.ConsultationStaleReason, ct);
        }
    }

    // A room nobody got into must not stay open once its appointment was cancelled or the doctor's join window closed.
    private async Task AbandonStuckAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var stuck in await consultations.ListStuckPendingAsync(now, BatchSize, ct))
        {
            var consultation = await consultations.FindForUpdateAsync(stuck.ConsultationId, ct);
            if (consultation is not { Status: ConsultationStatus.Scheduled or ConsultationStatus.Waiting })
            {
                continue;
            }

            End(consultation, ConsultationStatus.Abandoned, PlatformPolicy.ConsultationStaleReason, now);
            await unitOfWork.SaveChangesAsync(ct);
            await PublishClosedAsync(consultation, PlatformPolicy.ConsultationStaleReason, ct);
        }
    }

    private void End(Consultation consultation, ConsultationStatus outcome, string reason, DateTimeOffset now)
    {
        consultation.End(outcome, null, reason, now);
        consultations.AddEvent(ConsultationService.Event(consultation.Id, ConsultationEventKind.Ended, null, new { reason }, now));
    }

    private async Task PublishClosedAsync(Consultation consultation, string reason, CancellationToken ct)
    {
        await realtime.StateChangedAsync(consultation.ToDto(), ct);
        await realtime.ClosedAsync(consultation.Id, reason, ct);
    }
}

using TeleMed.Application.Abstractions;
using TeleMed.Application.Doctors;
using TeleMed.Application.Notifications.Templates;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Notifications;

// Appointment-related messages. Test appointments never notify anyone.
public sealed class AppointmentNotifier(INotificationService notifications, IDoctorRepository doctors, IAppLinks links)
{
    public async Task ConfirmedAsync(Appointment appointment, Payment payment, CancellationToken ct)
    {
        if (appointment.IsTest || await doctors.FindAsync(appointment.DoctorId, ct) is not { } doctor)
        {
            return;
        }

        await notifications.EnqueueAsync(
            appointment.PatientId,
            new BookingConfirmedModel(doctor.DisplayName, appointment.StartAt, payment.AmountCents, payment.Currency),
            $"appt:{appointment.Id}:confirmed",
            ct);
    }

    // Whoever cancelled already knows; the doctor only hears about bookings that had been confirmed.
    public async Task CancelledAsync(Appointment appointment, AppointmentStatus previousStatus, CancellationActor by, CancellationToken ct)
    {
        if (appointment.IsTest || await doctors.FindAsync(appointment.DoctorId, ct) is not { } doctor)
        {
            return;
        }

        if (by != CancellationActor.Patient)
        {
            await notifications.EnqueueAsync(
                appointment.PatientId, new AppointmentCancelledModel(doctor.DisplayName, appointment.StartAt), $"appt:{appointment.Id}:cancelled:patient", ct);
        }

        if (by != CancellationActor.Doctor && previousStatus == AppointmentStatus.Confirmed)
        {
            await notifications.EnqueueAsync(
                doctor.UserId, new AppointmentCancelledForDoctorModel(appointment.StartAt), $"appt:{appointment.Id}:cancelled:doctor", ct);
        }
    }

    public Task PaymentFailedAsync(Payment payment, CancellationToken ct) =>
        notifications.EnqueueAsync(
            payment.PatientId, new PaymentFailedModel(payment.AmountCents, payment.Currency), $"pay:{payment.Id}:failed:{payment.FailedAt:O}", ct);

    public async Task RescheduleRequestedAsync(RescheduleRequest request, Appointment appointment, Doctor doctor, CancellationToken ct)
    {
        if (appointment.IsTest)
        {
            return;
        }

        await notifications.EnqueueAsync(
            request.PatientId,
            new RescheduleRequestedModel(doctor.DisplayName, request.OriginalStartAt, request.ProposedStartAt),
            $"reschedule:{request.Id}:requested",
            ct);
    }

    public async Task RescheduleAcceptedAsync(RescheduleRequest request, Appointment appointment, CancellationToken ct)
    {
        if (appointment.IsTest || await doctors.FindAsync(appointment.DoctorId, ct) is not { } doctor)
        {
            return;
        }

        await notifications.EnqueueAsync(
            appointment.PatientId, new RescheduleConfirmedModel(doctor.DisplayName, appointment.StartAt), $"reschedule:{request.Id}:accepted:patient", ct);
        await notifications.EnqueueAsync(
            doctor.UserId, new RescheduleAcceptedForDoctorModel(appointment.StartAt), $"reschedule:{request.Id}:accepted:doctor", ct);
    }

    public async Task PaymentRefundedAsync(Appointment appointment, Payment payment, CancellationToken ct)
    {
        if (appointment.IsTest)
        {
            return;
        }

        await notifications.EnqueueAsync(
            payment.PatientId, new PaymentRefundedModel(payment.AmountCents, payment.Currency), $"pay:{payment.Id}:refunded", ct);
    }

    public async Task EarlyJoinOfferedAsync(Appointment next, Doctor doctor, CancellationToken ct)
    {
        if (next.IsTest)
        {
            return;
        }

        await notifications.EnqueueAsync(
            next.PatientId, new EarlyJoinOfferedModel(doctor.DisplayName, links.ConsultationJoin(next.Id)), $"appt:{next.Id}:early-join", ct);
    }

    public async Task DoctorRunningLateAsync(Appointment next, Guid lateConsultationId, CancellationToken ct)
    {
        if (next.IsTest || await doctors.FindAsync(next.DoctorId, ct) is not { } doctor)
        {
            return;
        }

        await notifications.EnqueueAsync(
            next.PatientId, new DoctorRunningLateModel(doctor.DisplayName), $"appt:{next.Id}:running-late:{lateConsultationId}", ct);
    }
}

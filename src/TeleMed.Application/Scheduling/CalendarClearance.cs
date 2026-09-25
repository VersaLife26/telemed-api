using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Payments;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Scheduling;

// Clears the appointments and reschedule holds that a new holiday or slot block would strand.
// Callers hold the transaction and the calendar lock of every doctor involved.
public sealed class CalendarClearance(PaymentLifecycle lifecycle, TimeProvider time)
{
    public async Task ClearAsync(
        IReadOnlyList<Appointment> appointments,
        IReadOnlyList<RescheduleRequest> proposals,
        bool cancelBooked,
        CancellationActor by,
        Guid byId,
        string reason,
        CancellationToken ct)
    {
        var affected = appointments.Select(a => a.Id).Union(proposals.Select(p => p.AppointmentId)).Count();
        if (affected == 0)
        {
            return;
        }

        if (!cancelBooked)
        {
            throw new ConflictException(
                "appointments_affected",
                $"{affected} appointment(s) are affected. Resend with cancelBooked=true to cancel them with a full refund.")
            {
                Extensions = { ["affectedAppointments"] = affected },
            };
        }

        foreach (var appointment in appointments.OrderBy(a => a.Id))
        {
            var payment = await lifecycle.LockPaymentAsync(appointment.Id, ct);
            await lifecycle.CancelAsync(appointment, payment, by, byId, reason, ct);
        }

        var now = time.GetUtcNow();
        foreach (var proposal in proposals.Where(p => p.Status == RescheduleStatus.Pending))
        {
            proposal.Decide(RescheduleStatus.Expired, by, byId, now);
        }
    }
}

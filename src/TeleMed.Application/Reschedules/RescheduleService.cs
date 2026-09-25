using FluentValidation;
using FluentValidation.Results;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Application.Notifications;
using TeleMed.Application.Payments;
using TeleMed.Application.Scheduling;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Reschedules;

public sealed class RescheduleService(
    ICurrentActor actor,
    IAppointmentRepository appointments,
    IRescheduleRepository reschedules,
    IDoctorRepository doctors,
    ISchedulingRepository scheduling,
    ICalendarLock calendar,
    IUnitOfWork unitOfWork,
    PaymentLifecycle lifecycle,
    AppointmentNotifier notifier,
    TimeProvider time)
{
    public async Task<RescheduleRequestDto> ProposeAsync(Guid appointmentId, ProposeRescheduleRequest request, CancellationToken ct)
    {
        var doctor = await doctors.MineWritableAsync(actor, ct);
        if (await appointments.FindAsync(appointmentId, ct) is not { } snapshot || snapshot.DoctorId != doctor.Id)
        {
            throw AppointmentNotFound();
        }

        var now = time.GetUtcNow();
        var proposedStart = request.ProposedStartAt.ToUniversalTime();
        if (proposedStart <= now)
        {
            throw new ValidationException([new ValidationFailure("proposedStartAt", "The proposed time must be in the future.")]);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await calendar.LockDoctorAsync(doctor.Id, ct);
        var appointment = await appointments.FindForUpdateAsync(appointmentId, ct) ?? throw AppointmentNotFound();
        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            throw NotReschedulable();
        }

        if (appointment.StartAt <= now)
        {
            throw new ConflictException("appointment_started", "The appointment has already started.");
        }

        if (proposedStart == appointment.StartAt)
        {
            throw new ValidationException([new ValidationFailure("proposedStartAt", "The proposed time must differ from the current time.")]);
        }

        if (await reschedules.FindPendingForAppointmentAsync(appointment.Id, ct) is not null)
        {
            throw new ConflictException("reschedule_pending", "This appointment already has a pending reschedule request.");
        }

        var proposedEnd = proposedStart + (appointment.EndAt - appointment.StartAt);
        var date = SlotPlanner.LocalDate(proposedStart, IanaTimeZone.Find(doctor.TimeZone));
        if ((await scheduling.ListHolidaysAsync(doctor.Id, date, date, ct)).Count > 0
            || (await scheduling.ListBusyAsync(doctor.Id, proposedStart, proposedEnd, appointment.Id, ct)).Count > 0)
        {
            throw new ConflictException("slot_unavailable", "You are not free at the proposed time.");
        }

        if (await appointments.PatientHasLiveOverlapAsync(appointment.PatientId, proposedStart, proposedEnd, appointment.Id, ct))
        {
            throw new ConflictException("patient_overlap", "The patient already has an appointment at the proposed time.");
        }

        var created = new RescheduleRequest
        {
            AppointmentId = appointment.Id,
            DoctorId = appointment.DoctorId,
            PatientId = appointment.PatientId,
            OriginalStartAt = appointment.StartAt,
            OriginalEndAt = appointment.EndAt,
            ProposedStartAt = proposedStart,
            ProposedEndAt = proposedEnd,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            RequestedByUserId = actor.RequireUserId(),
        };
        reschedules.Add(created);
        await notifier.RescheduleRequestedAsync(created, appointment, doctor, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return created.ToDto();
    }

    public async Task<IReadOnlyList<RescheduleRequestDto>> ListForAppointmentAsync(Guid appointmentId, CancellationToken ct)
    {
        var appointment = await appointments.FindAsync(appointmentId, ct) ?? throw AppointmentNotFound();
        var userId = actor.RequireUserId();
        var participant = actor.Role switch
        {
            UserRole.Patient => appointment.PatientId == userId,
            UserRole.Doctor => await doctors.FindByUserIdAsync(userId, ct) is { } doctor && doctor.Id == appointment.DoctorId,
            _ => false,
        };
        if (!participant)
        {
            throw AppointmentNotFound();
        }

        return (await reschedules.ListForAppointmentAsync(appointmentId, ct)).Select(r => r.ToDto()).ToList();
    }

    public async Task<PagedResult<RescheduleRequestDto>> ListAsync(RescheduleQuery query, CancellationToken ct)
    {
        var (items, total) = await reschedules.ListAsync(query.Status, query.Skip, query.PageSize, ct);
        return new PagedResult<RescheduleRequestDto>(items.Select(r => r.ToDto()).ToList(), query.Page, query.PageSize, total);
    }

    public Task<RescheduleDecisionDto> AcceptAsync(Guid id, CancellationToken ct) =>
        DecideAsync(id, accept: true, CancellationActor.Patient, actor.RequireUserId(), ct);

    public Task<RescheduleDecisionDto> DeclineAsync(Guid id, CancellationToken ct) =>
        DecideAsync(id, accept: false, CancellationActor.Patient, actor.RequireUserId(), ct);

    public Task<RescheduleDecisionDto> AdminAcceptAsync(Guid id, CancellationToken ct) =>
        DecideAsync(id, accept: true, CancellationActor.Admin, actor.RequireAdmin().Id, ct);

    public Task<RescheduleDecisionDto> AdminDeclineAsync(Guid id, CancellationToken ct) =>
        DecideAsync(id, accept: false, CancellationActor.Admin, actor.RequireAdmin().Id, ct);

    private async Task<RescheduleDecisionDto> DecideAsync(Guid id, bool accept, CancellationActor by, Guid byId, CancellationToken ct)
    {
        var snapshot = await reschedules.FindAsync(id, ct);
        if (snapshot is null || (by == CancellationActor.Patient && snapshot.PatientId != byId))
        {
            throw RequestNotFound();
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await calendar.LockDoctorAsync(snapshot.DoctorId, ct);
        var request = await reschedules.FindForUpdateAsync(id, ct) ?? throw RequestNotFound();
        if (request.Status != RescheduleStatus.Pending)
        {
            throw new ConflictException("reschedule_not_pending", "This reschedule request has already been decided.");
        }

        var appointment = await appointments.FindForUpdateAsync(request.AppointmentId, ct) ?? throw AppointmentNotFound();
        var now = time.GetUtcNow();
        if (accept)
        {
            if (appointment.Status != AppointmentStatus.Confirmed)
            {
                throw NotReschedulable();
            }

            if (request.ProposedStartAt <= now)
            {
                throw new ConflictException("proposed_time_passed", "The proposed time has already passed.");
            }

            request.Decide(RescheduleStatus.Accepted, by, byId, now);
            appointment.StartAt = request.ProposedStartAt;
            appointment.EndAt = request.ProposedEndAt;
            await lifecycle.ApplyRescheduleAsync(appointment, ct);
            await notifier.RescheduleAcceptedAsync(request, appointment, ct);
        }
        else
        {
            request.Decide(RescheduleStatus.Declined, by, byId, now);
            var payment = await lifecycle.LockPaymentAsync(appointment.Id, ct);
            await lifecycle.CancelWithFullRefundAsync(appointment, payment, by, byId, PlatformPolicy.RescheduleDeclinedReason, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new RescheduleDecisionDto(request.ToDto(), appointment.ToDto());
    }

    private static NotFoundException AppointmentNotFound() => new("Appointment not found.");

    private static NotFoundException RequestNotFound() => new("Reschedule request not found.");

    private static ConflictException NotReschedulable() => new("not_reschedulable", "Only confirmed appointments can be rescheduled.");
}

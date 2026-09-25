using FluentValidation;
using FluentValidation.Results;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Application.Reschedules;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Scheduling;

public sealed class SlotBlockService(
    ICurrentActor actor,
    IDoctorRepository doctors,
    ISchedulingRepository scheduling,
    IAppointmentRepository appointments,
    IRescheduleRepository reschedules,
    ICalendarLock calendar,
    CalendarClearance clearance,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public async Task<IReadOnlyList<SlotBlockDto>> ListAsync(Guid doctorId, SlotBlockQuery query, CancellationToken ct)
    {
        var doctor = await doctors.RequireAsync(doctorId, ct);
        return (await scheduling.ListSlotBlocksAsync(doctor.Id, query.From, query.To, ct)).Select(b => b.ToDto()).ToList();
    }

    public async Task<SlotBlockDto> AddAsync(Guid doctorId, CreateSlotBlockRequest request, CancellationToken ct)
    {
        var doctor = await doctors.RequireAsync(doctorId, ct);
        var now = time.GetUtcNow();
        if (request.EndAt <= now)
        {
            throw new ValidationException([new ValidationFailure("endAt", "The block must not lie entirely in the past.")]);
        }

        var block = new SlotBlock
        {
            DoctorId = doctor.Id,
            StartAt = request.StartAt.ToUniversalTime(),
            EndAt = request.EndAt.ToUniversalTime(),
            Reason = request.Reason.Trim(),
            CreatedByAdminId = actor.RequireAdmin().Id,
        };
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await calendar.LockDoctorAsync(doctor.Id, ct);
        var booked = (await appointments.ListLiveForUpdateAsync([doctor.Id], block.StartAt, block.EndAt, ct)).Where(a => a.StartAt > now).ToList();
        var proposals = (await reschedules.ListPendingForUpdateAsync([doctor.Id], block.StartAt, block.EndAt, ct)).Where(r => r.ProposedStartAt > now).ToList();
        await clearance.ClearAsync(booked, proposals, request.CancelBooked, CancellationActor.Admin, block.CreatedByAdminId, PlatformPolicy.SlotBlockedReason, ct);
        scheduling.AddSlotBlock(block);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return block.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var block = await scheduling.FindSlotBlockAsync(id, ct) ?? throw new NotFoundException("Slot block not found.");
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await calendar.LockDoctorAsync(block.DoctorId, ct);
        scheduling.RemoveSlotBlock(block);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}

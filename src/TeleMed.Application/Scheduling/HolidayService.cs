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

public sealed class HolidayService(
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
    public async Task<IReadOnlyList<HolidayDto>> ListMineAsync(HolidayQuery query, CancellationToken ct) =>
        await ListAsync((await doctors.MineAsync(actor, ct)).Id, query, ct);

    public async Task<HolidayDto> AddMineAsync(CreateHolidayRequest request, CancellationToken ct)
    {
        var doctor = await doctors.MineWritableAsync(actor, ct);
        return await AddAsync(doctor, request, CancellationActor.Doctor, actor.RequireUserId(), ct);
    }

    public async Task DeleteMineAsync(Guid id, CancellationToken ct)
    {
        var doctor = await doctors.MineWritableAsync(actor, ct);
        var holiday = await scheduling.FindHolidayAsync(id, ct);
        if (holiday is null || holiday.DoctorId != doctor.Id)
        {
            throw new NotFoundException("Holiday not found.");
        }

        await DeleteAsync(holiday, ct);
    }

    public async Task<IReadOnlyList<HolidayDto>> ListForDoctorAsync(Guid doctorId, HolidayQuery query, CancellationToken ct) =>
        await ListAsync((await doctors.RequireAsync(doctorId, ct)).Id, query, ct);

    public async Task<HolidayDto> AddForDoctorAsync(Guid doctorId, CreateHolidayRequest request, CancellationToken ct)
    {
        var doctor = await doctors.RequireAsync(doctorId, ct);
        return await AddAsync(doctor, request, CancellationActor.Admin, actor.RequireAdmin().Id, ct);
    }

    public Task<IReadOnlyList<HolidayDto>> ListPlatformAsync(HolidayQuery query, CancellationToken ct) => ListAsync(null, query, ct);

    public Task<HolidayDto> AddPlatformAsync(CreateHolidayRequest request, CancellationToken ct) =>
        AddAsync(null, request, CancellationActor.Admin, actor.RequireAdmin().Id, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct) =>
        await DeleteAsync(await scheduling.FindHolidayAsync(id, ct) ?? throw new NotFoundException("Holiday not found."), ct);

    private async Task<IReadOnlyList<HolidayDto>> ListAsync(Guid? doctorId, HolidayQuery query, CancellationToken ct) =>
        (await scheduling.ListHolidaysAsync(doctorId, query.From, query.To, ct)).Select(h => h.ToDto()).ToList();

    // Deleting a holiday later does not restore what this cancels.
    private async Task<HolidayDto> AddAsync(Doctor? doctor, CreateHolidayRequest request, CancellationActor by, Guid byId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var today = SlotPlanner.LocalDate(now, IanaTimeZone.Find(doctor?.TimeZone ?? PlatformPolicy.TimeZoneId));
        if (request.Date < today)
        {
            throw new ValidationException([new ValidationFailure("date", "The date must not be in the past.")]);
        }

        var holiday = new Holiday
        {
            DoctorId = doctor?.Id,
            Date = request.Date,
            Reason = request.Reason.Trim(),
            CreatedByUserId = by == CancellationActor.Doctor ? byId : null,
            CreatedByAdminId = by == CancellationActor.Admin ? byId : null,
        };
        // A calendar day in any zone lies within the UTC days either side of it.
        var from = new DateTimeOffset(request.Date.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = new DateTimeOffset(request.Date.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        if (doctor is null)
        {
            // Doctors without bookings are not locked below, so a booking for one of them must not slip in meanwhile.
            await calendar.LockPlatformAsync(ct);
        }

        IReadOnlyList<Guid> doctorIds = doctor is not null ? [doctor.Id] : await scheduling.ListBookedDoctorIdsAsync(from, to, ct);
        var zones = new Dictionary<Guid, TimeZoneInfo>();
        foreach (var id in doctorIds.Order())
        {
            await calendar.LockDoctorAsync(id, ct);
            zones[id] = IanaTimeZone.Find((doctor ?? await doctors.RequireAsync(id, ct)).TimeZone);
        }

        bool OnHoliday(Guid doctorId, DateTimeOffset start) => start > now && SlotPlanner.LocalDate(start, zones[doctorId]) == request.Date;
        var booked = (await appointments.ListLiveForUpdateAsync(doctorIds, from, to, ct)).Where(a => OnHoliday(a.DoctorId, a.StartAt)).ToList();
        var proposals = (await reschedules.ListPendingForUpdateAsync(doctorIds, from, to, ct)).Where(r => OnHoliday(r.DoctorId, r.ProposedStartAt)).ToList();
        await clearance.ClearAsync(booked, proposals, request.CancelBooked, by, byId, PlatformPolicy.DoctorOnLeaveReason, ct);

        scheduling.AddHoliday(holiday);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return holiday.ToDto();
    }

    private async Task DeleteAsync(Holiday holiday, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        if (holiday.DoctorId is { } id)
        {
            await calendar.LockDoctorAsync(id, ct);
        }

        scheduling.RemoveHoliday(holiday);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}

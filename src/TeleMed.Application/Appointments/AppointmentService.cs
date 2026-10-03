using TeleMed.Application.Abstractions;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Application.Payments;
using TeleMed.Application.Scheduling;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Appointments;

public sealed class AppointmentService(
    ICurrentActor actor,
    IAppointmentRepository appointments,
    IPaymentRepository payments,
    IDoctorRepository doctors,
    ISchedulingRepository scheduling,
    ICalendarLock calendar,
    IUnitOfWork unitOfWork,
    PaymentLifecycle lifecycle,
    TimeProvider time)
{
    public async Task<AppointmentDto> BookAsync(BookAppointmentRequest request, CancellationToken ct)
    {
        var patientId = actor.RequireUserId();
        var doctor = await doctors.FindListedAsync(request.DoctorId, ct) ?? throw new NotFoundException("Doctor not found.");
        if (doctor.UserId == patientId)
        {
            throw new ConflictException("self_booking", "You cannot book an appointment with yourself.");
        }

        if (!doctor.AcceptsNewPatients)
        {
            throw new ConflictException("not_accepting_patients", "This doctor is not accepting new patients.");
        }

        if (doctor.FeeCents <= 0)
        {
            throw new ConflictException("fee_not_set", "This doctor has no consultation fee set.");
        }

        var policy = doctor.ToPolicy();
        var now = time.GetUtcNow();
        var date = SlotPlanner.LocalDate(request.StartAt, policy.TimeZone);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await calendar.LockDoctorAsync(doctor.Id, ct);
        var hours = await scheduling.ListWorkingHoursAsync(doctor.Id, ct);
        var holidays = await scheduling.ListHolidaysAsync(doctor.Id, date, date, ct);
        var busy = await scheduling.ListBusyAsync(
            doctor.Id,
            new DateTimeOffset(date.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(date.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            null,
            ct);
        var slot = SlotPlanner.Plan(policy, hours.Select(h => h.ToWindow()).ToList(), holidays.Select(h => h.Date).ToHashSet(), busy, date, date, now)
            .FirstOrDefault(s => s.Available && s.StartAt == request.StartAt);
        if (slot == default)
        {
            throw new ConflictException("slot_unavailable", "This time is no longer available.");
        }

        var appointment = new Appointment
        {
            PatientId = patientId,
            DoctorId = doctor.Id,
            StartAt = slot.StartAt,
            EndAt = slot.EndAt,
            FeeCents = doctor.FeeCents,
            Currency = doctor.Currency,
            Intake = new AppointmentIntake(request.Intake.Symptoms.Trim(), Blank(request.Intake.VisitRelation)),
            VisitPatientName = request.VisitPatient.Name.Trim(),
            VisitPatientDateOfBirth = request.VisitPatient.DateOfBirth,
            VisitPatientSex = request.VisitPatient.Sex,
            VisitPatientWeightKg = request.VisitPatient.WeightKg,
            VisitPatientAllergies = Blank(request.VisitPatient.Allergies),
            PaymentDueAt = now + PlatformPolicy.PaymentWindow,
        };
        var payment = new Payment
        {
            AppointmentId = appointment.Id,
            PatientId = patientId,
            DoctorId = doctor.Id,
            Currency = doctor.Currency,
            GrossCents = doctor.FeeCents,
        };
        await lifecycle.SetPriceAsync(payment, discountCents: 0, promoCode: null, ct);
        appointments.Add(appointment);
        payments.Add(payment);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return appointment.ToDto();
    }

    public async Task<PagedResult<AppointmentDto>> ListAsync(AppointmentQuery query, CancellationToken ct)
    {
        // Participants see their instant test meetings too (flagged isTest), so they can find and join them.
        var filter = new AppointmentFilter(null, null, query.Status, query.From, query.To, query.Skip, query.PageSize, IncludeTest: true);
        filter = actor.Role == UserRole.Doctor
            ? filter with { DoctorId = (await doctors.MineAsync(actor, ct)).Id }
            : filter with { PatientId = actor.RequireUserId() };
        var (items, total) = await appointments.ListAsync(filter, ct);
        return new PagedResult<AppointmentDto>(items.Select(a => a.ToDto()).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<AppointmentDto> GetAsync(Guid id, CancellationToken ct)
    {
        var appointment = await appointments.FindAsync(id, ct) ?? throw NotFound();
        await ParticipantAsync(appointment, ct);
        return appointment.ToDto();
    }

    public async Task<LastVisitDetailsDto> GetLastVisitDetailsAsync(CancellationToken ct) =>
        await appointments.FindLastSelfVisitAsync(actor.RequireUserId(), ct) is { } last
            ? new LastVisitDetailsDto(last.VisitPatientName, last.VisitPatientDateOfBirth, last.VisitPatientSex, last.VisitPatientWeightKg, last.VisitPatientAllergies)
            : new LastVisitDetailsDto(null, null, null, null, null);

    public async Task<AppointmentDto> CancelAsync(Guid id, CancelAppointmentRequest? request, CancellationToken ct)
    {
        var snapshot = await appointments.FindAsync(id, ct) ?? throw NotFound();
        var by = await ParticipantAsync(snapshot, ct);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await calendar.LockDoctorAsync(snapshot.DoctorId, ct);
        var appointment = await appointments.FindForUpdateAsync(id, ct) ?? throw NotFound();
        var payment = await lifecycle.LockPaymentAsync(id, ct);
        await lifecycle.CancelAsync(appointment, payment, by, actor.RequireUserId(), Blank(request?.Reason), ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return appointment.ToDto();
    }

    public async Task<AppointmentDto> CompleteAsync(Guid id, CancellationToken ct) =>
        await FinishAsync(id, AppointmentStatus.Completed, ct);

    public async Task<AppointmentDto> MarkNoShowAsync(Guid id, CancellationToken ct) =>
        await FinishAsync(id, AppointmentStatus.NoShow, ct);

    private async Task<AppointmentDto> FinishAsync(Guid id, AppointmentStatus to, CancellationToken ct)
    {
        var appointment = await appointments.FindForUpdateAsync(id, ct) ?? throw NotFound();
        if (await ParticipantAsync(appointment, ct) != CancellationActor.Doctor)
        {
            throw NotFound();
        }

        if (!AppointmentTransitions.IsAllowed(appointment.Status, to))
        {
            throw new ConflictException("invalid_transition", $"Only confirmed appointments can be marked {(to == AppointmentStatus.Completed ? "completed" : "no-show")}.");
        }

        if (time.GetUtcNow() < appointment.StartAt)
        {
            throw new ConflictException("not_started", "The appointment has not started yet.");
        }

        if (to == AppointmentStatus.Completed)
        {
            lifecycle.Complete(appointment);
        }
        else
        {
            var payment = await lifecycle.LockPaymentAsync(id, ct);
            await lifecycle.MarkPatientNoShowAsync(appointment, payment, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return appointment.ToDto();
    }

    // Anyone but the two participants gets a 404 so appointment ids cannot be probed.
    private async Task<CancellationActor> ParticipantAsync(Appointment appointment, CancellationToken ct)
    {
        var userId = actor.RequireUserId();
        if (actor.Role == UserRole.Patient && appointment.PatientId == userId)
        {
            return CancellationActor.Patient;
        }

        if (actor.Role == UserRole.Doctor && await doctors.FindByUserIdAsync(userId, ct) is { } doctor && doctor.Id == appointment.DoctorId)
        {
            return CancellationActor.Doctor;
        }

        throw NotFound();
    }

    private static NotFoundException NotFound() => new("Appointment not found.");

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

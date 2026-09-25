using TeleMed.Application.Abstractions;
using TeleMed.Application.Admin.Audit;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Payments;
using TeleMed.Application.Reschedules;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Appointments;

public sealed class AdminAppointmentService(
    ICurrentActor actor,
    IAppointmentRepository appointments,
    IPaymentRepository payments,
    IRescheduleRepository reschedules,
    IAuditLogRepository audit,
    ICalendarLock calendar,
    IUnitOfWork unitOfWork,
    PaymentLifecycle lifecycle)
{
    public async Task<PagedResult<AppointmentDto>> ListAsync(AdminAppointmentQuery query, CancellationToken ct)
    {
        var filter = new AppointmentFilter(query.PatientId, query.DoctorId, query.Status, query.From, query.To, query.Skip, query.PageSize, query.IncludeTest);
        var (items, total) = await appointments.ListAsync(filter, ct);
        return new PagedResult<AppointmentDto>(items.Select(a => a.ToDto()).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<AdminAppointmentDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var appointment = await appointments.FindAsync(id, ct) ?? throw NotFound();
        var payment = await payments.FindByAppointmentAsync(id, ct);
        var refunds = payment is null ? [] : await payments.ListRefundsAsync([payment.Id], ct);
        return new AdminAppointmentDetailDto(
            appointment.ToDto(),
            payment?.ToDto(refunds),
            (await reschedules.ListForAppointmentAsync(id, ct)).Select(r => r.ToDto()).ToList());
    }

    public async Task<IReadOnlyList<AuditEntryDto>> ListAuditAsync(Guid id, CancellationToken ct)
    {
        _ = await appointments.FindAsync(id, ct) ?? throw NotFound();
        return (await audit.ListForAppointmentAsync(id, ct)).Select(a => a.ToDto()).ToList();
    }

    public async Task<AppointmentDto> CancelAsync(Guid id, AdminCancelAppointmentRequest request, CancellationToken ct)
    {
        var admin = actor.RequireAdmin();
        var snapshot = await appointments.FindAsync(id, ct) ?? throw NotFound();

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await calendar.LockDoctorAsync(snapshot.DoctorId, ct);
        var appointment = await appointments.FindForUpdateAsync(id, ct) ?? throw NotFound();
        var payment = await lifecycle.LockPaymentAsync(id, ct);
        await lifecycle.CancelAsync(appointment, payment, CancellationActor.Admin, admin.Id, request.Reason.Trim(), ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return appointment.ToDto();
    }

    private static NotFoundException NotFound() => new("Appointment not found.");
}

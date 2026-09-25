using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Payments;
using TeleMed.Application.Reschedules;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Appointments;

public sealed record AdminAppointmentQuery : PageQuery
{
    public AppointmentStatus? Status { get; init; }
    public Guid? DoctorId { get; init; }
    public Guid? PatientId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public bool IncludeTest { get; init; }
}

public sealed record AdminAppointmentDetailDto(AppointmentDto Appointment, PaymentDto? Payment, IReadOnlyList<RescheduleRequestDto> RescheduleRequests);

public sealed record AdminCancelAppointmentRequest(string Reason);

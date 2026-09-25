using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Consultations;

public interface IConsultationRepository
{
    void Add(Consultation consultation);
    Task<Consultation?> FindAsync(Guid id, CancellationToken ct);
    Task<Consultation?> FindByAppointmentAsync(Guid appointmentId, CancellationToken ct);
    Task<Consultation?> FindForUpdateAsync(Guid id, CancellationToken ct);
    Task<Consultation?> FindForUpdateByAppointmentAsync(Guid appointmentId, CancellationToken ct);
    // Inserts the row if the appointment has none yet, then locks it (FOR UPDATE) for the rest of the transaction; tracked.
    Task<Consultation> LockOrCreateAsync(Guid appointmentId, DateTimeOffset now, CancellationToken ct);

    void AddEvent(ConsultationEvent consultationEvent);
    // Newest first.
    Task<IReadOnlyList<string?>> ListRecentEventDataAsync(Guid consultationId, ConsultationEventKind kind, UserRole role, int take, CancellationToken ct);

    void AddMessage(ConsultationMessage message);
    Task<(IReadOnlyList<ConsultationMessage> Items, long Total)> ListMessagesAsync(Guid consultationId, int skip, int take, CancellationToken ct);

    // The doctor's active consultations, plus waiting ones booked before startAt.
    Task<int> CountAheadAsync(Guid doctorId, Guid consultationId, DateTimeOffset startAt, CancellationToken ct);
    // Durations of the doctor's most recent finished non-test consultations.
    Task<IReadOnlyList<int>> ListRecentDurationsAsync(Guid doctorId, int take, CancellationToken ct);

    // Confirmed appointments that have ended without both participants ever joining.
    Task<IReadOnlyList<UnattendedAppointment>> ListUnattendedEndedAsync(DateTimeOffset now, int limit, CancellationToken ct);
    Task<IReadOnlyList<OverrunConsultation>> ListOverrunActiveAsync(DateTimeOffset now, int limit, CancellationToken ct);
    // Active consultations with no start, join or leave since the cutoff.
    Task<IReadOnlyList<IdleConsultation>> ListIdleActiveAsync(DateTimeOffset cutoff, int limit, CancellationToken ct);
    // Scheduled or waiting consultations whose appointment is no longer confirmed, or ended more than the doctor's join grace ago.
    Task<IReadOnlyList<IdleConsultation>> ListStuckPendingAsync(DateTimeOffset now, int limit, CancellationToken ct);
}

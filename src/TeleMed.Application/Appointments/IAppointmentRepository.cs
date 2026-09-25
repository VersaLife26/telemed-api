using TeleMed.Domain.Entities;

namespace TeleMed.Application.Appointments;

public interface IAppointmentRepository
{
    void Add(Appointment appointment);
    Task<Appointment?> FindAsync(Guid id, CancellationToken ct);
    // Tracked; load only after the calendar lock is held so the row is not stale.
    Task<Appointment?> FindForUpdateAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<Appointment> Items, long Total)> ListAsync(AppointmentFilter filter, CancellationToken ct);
    Task<Appointment?> FindLastSelfVisitAsync(Guid patientId, CancellationToken ct);
    Task<bool> PatientHasLiveOverlapAsync(Guid patientId, DateTimeOffset start, DateTimeOffset end, Guid? exceptAppointmentId, CancellationToken ct);
    // Tracked pending-payment and confirmed non-test appointments overlapping [from, to).
    Task<IReadOnlyList<Appointment>> ListLiveForUpdateAsync(IReadOnlyCollection<Guid> doctorIds, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    // Doctors of the patient's pending-payment and confirmed non-test appointments starting after `after`.
    Task<IReadOnlyList<Guid>> ListLiveDoctorIdsForPatientAsync(Guid patientId, DateTimeOffset after, CancellationToken ct);
    // Tracked; the patient's pending-payment and confirmed non-test appointments starting after `after`.
    Task<IReadOnlyList<Appointment>> ListLiveForPatientForUpdateAsync(Guid patientId, DateTimeOffset after, CancellationToken ct);
    Task<IReadOnlyList<ExpiredBooking>> ListExpiredUnpaidAsync(DateTimeOffset now, int limit, CancellationToken ct);
    // Confirmed non-test appointments starting in (from, to].
    Task<IReadOnlyList<ReminderCandidate>> ListReminderCandidatesAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    // The doctor's earliest confirmed appointment other than exceptAppointmentId starting in (startsAfter, startsBefore].
    Task<Appointment?> FindNextConfirmedAsync(Guid doctorId, Guid exceptAppointmentId, DateTimeOffset startsAfter, DateTimeOffset startsBefore, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ListConfirmedEndedBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken ct);
}

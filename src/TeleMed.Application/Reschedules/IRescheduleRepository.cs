using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Reschedules;

public interface IRescheduleRepository
{
    void Add(RescheduleRequest request);
    Task<RescheduleRequest?> FindAsync(Guid id, CancellationToken ct);
    // Tracked; load only after the calendar lock is held so the row is not stale.
    Task<RescheduleRequest?> FindForUpdateAsync(Guid id, CancellationToken ct);
    Task<RescheduleRequest?> FindPendingForAppointmentAsync(Guid appointmentId, CancellationToken ct);
    // Tracked pending requests whose proposed time overlaps [from, to).
    Task<IReadOnlyList<RescheduleRequest>> ListPendingForUpdateAsync(IReadOnlyCollection<Guid> doctorIds, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<IReadOnlyList<RescheduleRequest>> ListForAppointmentAsync(Guid appointmentId, CancellationToken ct);
    Task<(IReadOnlyList<RescheduleRequest> Items, long Total)> ListAsync(RescheduleStatus? status, int skip, int take, CancellationToken ct);
    Task<IReadOnlyList<ExpiredReschedule>> ListExpiredPendingAsync(DateTimeOffset now, int limit, CancellationToken ct);
}

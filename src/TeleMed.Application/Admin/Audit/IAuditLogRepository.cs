using TeleMed.Domain.Entities;

namespace TeleMed.Application.Admin.Audit;

public interface IAuditLogRepository
{
    // The appointment's own rows plus those of its payment, refunds and reschedule requests, oldest first.
    Task<IReadOnlyList<AuditLog>> ListForAppointmentAsync(Guid appointmentId, CancellationToken ct);
    Task<IReadOnlyList<AuditLog>> ListByActorAsync(Guid actorId, int limit, CancellationToken ct);
    Task<(IReadOnlyList<AuditLog> Items, long Total)> ListAsync(AuditFilter filter, int skip, int take, CancellationToken ct);
    IAsyncEnumerable<AuditLog> StreamAsync(AuditFilter filter, CancellationToken ct);
}

public sealed record AuditFilter(Guid? ActorId, string? EntityType, string? EntityId, DateTimeOffset? From, DateTimeOffset? To);

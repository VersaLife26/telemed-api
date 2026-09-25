using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Disputes;

public interface IDisputeRepository
{
    void Add(Dispute dispute);
    void AddComment(DisputeComment comment);
    Task<Dispute?> FindAsync(Guid id, CancellationToken ct);
    Task<Dispute?> FindForUpdateAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<Dispute> Items, long Total)> ListAsync(DisputeStatus? status, Guid? assignedAdminId, Guid? appointmentId, int skip, int take, CancellationToken ct);
    Task<IReadOnlyList<DisputeComment>> ListCommentsAsync(Guid disputeId, CancellationToken ct);
}

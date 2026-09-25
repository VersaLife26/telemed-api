using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Admin.Disputes;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class DisputeRepository(AppDbContext db) : IDisputeRepository
{
    public void Add(Dispute dispute) => db.Disputes.Add(dispute);

    public void AddComment(DisputeComment comment) => db.DisputeComments.Add(comment);

    public Task<Dispute?> FindAsync(Guid id, CancellationToken ct) => db.Disputes.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct);

    public Task<Dispute?> FindForUpdateAsync(Guid id, CancellationToken ct) => db.Disputes.SingleOrDefaultAsync(d => d.Id == id, ct);

    public async Task<(IReadOnlyList<Dispute> Items, long Total)> ListAsync(
        DisputeStatus? status, Guid? assignedAdminId, Guid? appointmentId, int skip, int take, CancellationToken ct)
    {
        var query = db.Disputes.AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(d => d.Status == s);
        }

        if (assignedAdminId is { } assignee)
        {
            query = query.Where(d => d.AssignedAdminId == assignee);
        }

        if (appointmentId is { } appointment)
        {
            query = query.Where(d => d.AppointmentId == appointment);
        }

        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(d => d.CreatedAt).ThenBy(d => d.Id).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<DisputeComment>> ListCommentsAsync(Guid disputeId, CancellationToken ct) =>
        await db.DisputeComments.AsNoTracking().Where(c => c.DisputeId == disputeId).OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).ToListAsync(ct);
}

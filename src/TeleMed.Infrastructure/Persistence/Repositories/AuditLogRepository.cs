using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Admin.Audit;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class AuditLogRepository(AppDbContext db) : IAuditLogRepository
{
    public void Add(AuditLog log) => db.AuditLogs.Add(log);

    public async Task<IReadOnlyList<AuditLog>> ListForAppointmentAsync(Guid appointmentId, CancellationToken ct)
    {
        var paymentIds = await db.Payments.Where(p => p.AppointmentId == appointmentId).Select(p => p.Id).ToListAsync(ct);
        var refundIds = await db.Refunds.Where(r => paymentIds.Contains(r.PaymentId)).Select(r => r.Id).ToListAsync(ct);
        var rescheduleIds = await db.RescheduleRequests.Where(r => r.AppointmentId == appointmentId).Select(r => r.Id).ToListAsync(ct);

        // The interceptor keys rows by table name and the primary key's string form.
        var appointment = appointmentId.ToString();
        var payments = paymentIds.Select(id => id.ToString()).ToList();
        var refunds = refundIds.Select(id => id.ToString()).ToList();
        var reschedules = rescheduleIds.Select(id => id.ToString()).ToList();
        var (appointmentTable, paymentTable, refundTable, rescheduleTable) = (Table<Appointment>(), Table<Payment>(), Table<Refund>(), Table<RescheduleRequest>());
        return await db.AuditLogs.AsNoTracking()
            .Where(a => (a.EntityType == appointmentTable && a.EntityId == appointment)
                || (a.EntityType == paymentTable && payments.Contains(a.EntityId))
                || (a.EntityType == refundTable && refunds.Contains(a.EntityId))
                || (a.EntityType == rescheduleTable && reschedules.Contains(a.EntityId)))
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> ListByActorAsync(Guid actorId, int limit, CancellationToken ct) =>
        await db.AuditLogs.AsNoTracking()
            .Where(a => a.ActorId == actorId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<AuditLog> Items, long Total)> ListAsync(AuditFilter filter, int skip, int take, CancellationToken ct)
    {
        var query = Filtered(filter);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public IAsyncEnumerable<AuditLog> StreamAsync(AuditFilter filter, CancellationToken ct) =>
        Filtered(filter).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).AsAsyncEnumerable();

    private IQueryable<AuditLog> Filtered(AuditFilter filter)
    {
        var query = db.AuditLogs.AsNoTracking();
        if (filter.ActorId is { } actorId)
        {
            query = query.Where(a => a.ActorId == actorId);
        }

        if (filter.EntityType is { } entityType)
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        if (filter.EntityId is { } entityId)
        {
            query = query.Where(a => a.EntityId == entityId);
        }

        if (filter.From is { } from)
        {
            var utc = from.ToUniversalTime();
            query = query.Where(a => a.CreatedAt >= utc);
        }

        if (filter.To is { } to)
        {
            var utc = to.ToUniversalTime();
            query = query.Where(a => a.CreatedAt < utc);
        }

        return query;
    }

    private string Table<T>() => db.Model.FindEntityType(typeof(T))!.GetTableName()!;
}

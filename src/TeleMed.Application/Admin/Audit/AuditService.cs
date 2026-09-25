using TeleMed.Application.Common;

namespace TeleMed.Application.Admin.Audit;

public sealed class AuditService(IAuditLogRepository audit)
{
    private static readonly string[] Header =
        ["id", "created_at", "actor_type", "actor_id", "actor_email", "action", "entity_type", "entity_id", "changes", "ip", "request_id"];

    public async Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, CancellationToken ct)
    {
        var (items, total) = await audit.ListAsync(Filter(query), query.Skip, query.PageSize, ct);
        return new PagedResult<AuditEntryDto>(items.Select(a => a.ToDto()).ToList(), query.Page, query.PageSize, total);
    }

    public Task WriteCsvAsync(AuditExportQuery query, Stream output, CancellationToken ct) =>
        Csv.WriteAsync(
            output,
            Header,
            audit.StreamAsync(Filter(query), ct),
            a => [a.Id, a.CreatedAt, a.ActorType, a.ActorId, a.ActorEmail, a.Action, a.EntityType, a.EntityId, a.Changes, a.Ip?.ToString(), a.RequestId],
            ct);

    private static AuditFilter Filter(IAuditQuery query) =>
        new(query.ActorId, Blank(query.EntityType), Blank(query.EntityId), query.From, query.To);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Testing;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class CapturedMessageRepository(AppDbContext db) : ICapturedMessageRepository
{
    public void Add(CapturedMessage message) => db.CapturedMessages.Add(message);

    public async Task<IReadOnlyList<CapturedMessage>> ListNewestFirstAsync(
        string? recipient, MessageChannel? channel, int limit, CancellationToken ct)
    {
        var query = db.CapturedMessages.AsNoTracking();
        if (!string.IsNullOrEmpty(recipient))
        {
            query = query.Where(m => m.Recipient == recipient);
        }

        if (channel is not null)
        {
            query = query.Where(m => m.Channel == channel);
        }

        return await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(limit).ToListAsync(ct);
    }

    public async Task RemoveAllAsync(CancellationToken ct) =>
        db.CapturedMessages.RemoveRange(await db.CapturedMessages.ToListAsync(ct));

    public async Task<IReadOnlyList<CapturedMessage>> ListCreatedBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken ct) =>
        await db.CapturedMessages.Where(m => m.CreatedAt < cutoff).OrderBy(m => m.Id).Take(limit).ToListAsync(ct);

    public void RemoveRange(IEnumerable<CapturedMessage> messages) => db.CapturedMessages.RemoveRange(messages);
}

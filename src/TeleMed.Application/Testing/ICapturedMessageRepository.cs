using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Testing;

public interface ICapturedMessageRepository
{
    void Add(CapturedMessage message);
    Task<IReadOnlyList<CapturedMessage>> ListNewestFirstAsync(string? recipient, MessageChannel? channel, int limit, CancellationToken ct);
    Task RemoveAllAsync(CancellationToken ct);
    Task<IReadOnlyList<CapturedMessage>> ListCreatedBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken ct);
    void RemoveRange(IEnumerable<CapturedMessage> messages);
}

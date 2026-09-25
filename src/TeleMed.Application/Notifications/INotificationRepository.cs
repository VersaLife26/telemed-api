using TeleMed.Domain.Entities;

namespace TeleMed.Application.Notifications;

public interface INotificationRepository
{
    void Add(Notification notification);
    // Includes rows staged but not yet saved in this unit of work.
    Task<IReadOnlySet<string>> ExistingDedupeKeysAsync(IReadOnlyCollection<string> keys, CancellationToken ct);
    // Due pending rows, and sending rows whose lease ran out, locked FOR UPDATE SKIP LOCKED for the current transaction; tracked.
    Task<IReadOnlyList<Notification>> ClaimDueAsync(DateTimeOffset now, int limit, CancellationToken ct);
    Task<IReadOnlyList<Notification>> ListSentWithBodyBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken ct);
}

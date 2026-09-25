using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Abstractions;

namespace TeleMed.Infrastructure.Persistence;

internal sealed class CalendarLock(AppDbContext db) : ICalendarLock
{
    private const string PlatformKey = "cal:platform";

    public async Task LockDoctorAsync(Guid doctorId, CancellationToken ct)
    {
        EnsureTransaction();
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock_shared(hashtext({PlatformKey}))", ct);
        var key = $"cal:{doctorId}";
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", ct);
    }

    public async Task LockPlatformAsync(CancellationToken ct)
    {
        EnsureTransaction();
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({PlatformKey}))", ct);
    }

    private void EnsureTransaction()
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The calendar lock is transaction-scoped; begin a transaction first.");
        }
    }
}

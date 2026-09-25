using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Jobs;
using TeleMed.Domain.Entities;
using TeleMed.Infrastructure.BackgroundJobs;

namespace TeleMed.Infrastructure.Persistence;

internal sealed class UnitOfWork(AppDbContext db, JobWakeup<NotificationDispatcherJob> dispatcher) : IUnitOfWork
{
    private bool _wakeDispatcherOnCommit;

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        var notificationsStaged = db.ChangeTracker.Entries<Notification>().Any(e => e.State == EntityState.Added);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (PostgresErrorMapper.Map(ex) is { } conflict)
        {
            throw conflict;
        }

        if (!notificationsStaged)
        {
            return;
        }

        // The dispatcher reads committed rows only, so inside a transaction the wake-up waits for the commit.
        if (db.Database.CurrentTransaction is null)
        {
            dispatcher.Signal();
        }
        else
        {
            _wakeDispatcherOnCommit = true;
        }
    }

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        new Transaction(this, await db.Database.BeginTransactionAsync(ct));

    private void OnCommitted()
    {
        if (_wakeDispatcherOnCommit)
        {
            _wakeDispatcherOnCommit = false;
            dispatcher.Signal();
        }
    }

    private sealed class Transaction(UnitOfWork owner, IDbContextTransaction inner) : ITransaction
    {
        public async Task CommitAsync(CancellationToken ct = default)
        {
            await inner.CommitAsync(ct);
            owner.OnCommitted();
        }

        public ValueTask DisposeAsync()
        {
            owner._wakeDispatcherOnCommit = false;
            return inner.DisposeAsync();
        }
    }
}

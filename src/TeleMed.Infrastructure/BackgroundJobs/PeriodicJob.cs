using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TeleMed.Application.Jobs;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.BackgroundJobs;

internal sealed class PeriodicJob<TJob>(
    IServiceScopeFactory scopes,
    IEnumerable<JobWakeup<TJob>> wakeups,
    IOptionsMonitor<JobOptions> options,
    IOptions<DatabaseOptions> database,
    TimeProvider time,
    ILogger<PeriodicJob<TJob>> logger) : BackgroundService
    where TJob : IBackgroundJob
{
    public static string Name { get; } = typeof(TJob).Name.EndsWith("Job", StringComparison.Ordinal) ? typeof(TJob).Name[..^3] : typeof(TJob).Name;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Get(Name);
        if (!settings.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(settings.Interval, time);
        var wakeup = wakeups.SingleOrDefault();
        Task<bool>? tick = null;
        Task? wake = null;
        try
        {
            while (true)
            {
                await RunOnceAsync(stoppingToken);

                // A PeriodicTimer allows one pending wait, so whichever of tick/wake did not fire is kept for the next round.
                tick ??= timer.WaitForNextTickAsync(stoppingToken).AsTask();
                wake ??= wakeup?.WaitAsync(stoppingToken) ?? Task.Delay(Timeout.Infinite, stoppingToken);
                if (await Task.WhenAny(tick, wake) == wake)
                {
                    await wake;
                    wake = null;
                    continue;
                }

                if (!await tick)
                {
                    return;
                }

                tick = null;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    // A session-level advisory lock on a dedicated connection keeps a second instance from running the same job concurrently.
    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var connection = new NpgsqlConnection(database.Value.ConnectionString);
            await connection.OpenAsync(ct);
            if (!await AdvisoryAsync(connection, "pg_try_advisory_lock", ct))
            {
                return;
            }

            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<TJob>().RunAsync(ct);
            }
            finally
            {
                await AdvisoryAsync(connection, "pg_advisory_unlock", CancellationToken.None);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Background job {Job} failed", Name);
        }
    }

    private static async Task<bool> AdvisoryAsync(NpgsqlConnection connection, string function, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand($"SELECT {function}(hashtext(@key))", connection);
        command.Parameters.AddWithValue("key", $"job:{Name}");
        return await command.ExecuteScalarAsync(ct) is true;
    }
}

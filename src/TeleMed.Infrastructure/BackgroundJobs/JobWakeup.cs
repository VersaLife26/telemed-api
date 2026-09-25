using System.Threading.Channels;
using TeleMed.Application.Jobs;

namespace TeleMed.Infrastructure.BackgroundJobs;

// Lets a job run before its next tick. Signals collapse into one pending wake-up.
internal sealed class JobWakeup<TJob>
    where TJob : IBackgroundJob
{
    private readonly Channel<bool> _signals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Signal() => _signals.Writer.TryWrite(true);

    public async Task WaitAsync(CancellationToken ct)
    {
        await _signals.Reader.WaitToReadAsync(ct);
        _signals.Reader.TryRead(out _);
    }
}

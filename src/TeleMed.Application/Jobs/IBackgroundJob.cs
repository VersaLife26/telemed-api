namespace TeleMed.Application.Jobs;

public interface IBackgroundJob
{
    Task RunAsync(CancellationToken ct);
}

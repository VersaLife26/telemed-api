using TeleMed.Application.Abstractions;
using TeleMed.Application.Auth;
using TeleMed.Application.Notifications;
using TeleMed.Application.Testing;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Jobs;

public sealed class HousekeepingJob(
    IRefreshTokenRepository refreshTokens,
    IOtpChallengeRepository otpChallenges,
    IPasswordResetTokenRepository passwordResets,
    ICapturedMessageRepository capturedMessages,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IBackgroundJob
{
    private const int BatchSize = 500;

    public async Task RunAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var cutoff = now - PlatformPolicy.HousekeepingRetention;
        await DrainAsync(() => refreshTokens.ListPurgeableAsync(cutoff, BatchSize, ct), refreshTokens.RemoveRange, ct);
        await DrainAsync(() => otpChallenges.ListCreatedBeforeAsync(cutoff, BatchSize, ct), otpChallenges.RemoveRange, ct);
        await DrainAsync(() => passwordResets.ListCreatedBeforeAsync(cutoff, BatchSize, ct), passwordResets.RemoveRange, ct);
        await DrainAsync(() => capturedMessages.ListCreatedBeforeAsync(cutoff, BatchSize, ct), capturedMessages.RemoveRange, ct);
        await DrainAsync(
            () => notifications.ListSentWithBodyBeforeAsync(now - PlatformPolicy.NotificationBodyRetention, BatchSize, ct),
            batch =>
            {
                foreach (var notification in batch)
                {
                    notification.Body = null;
                }
            },
            ct);
    }

    private async Task DrainAsync<T>(Func<Task<IReadOnlyList<T>>> next, Action<IReadOnlyList<T>> apply, CancellationToken ct)
    {
        IReadOnlyList<T> batch;
        do
        {
            batch = await next();
            if (batch.Count == 0)
            {
                return;
            }

            apply(batch);
            await unitOfWork.SaveChangesAsync(ct);
        }
        while (batch.Count == BatchSize);
    }
}

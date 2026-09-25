using System.Text.RegularExpressions;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Notifications;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Jobs;

// Claims due rows in a short transaction, sends outside it, then records each outcome. A claimed row carries a
// lease in next_attempt_at, so a crash between claim and outcome only delays that row until the lease runs out.
public sealed partial class NotificationDispatcherJob(
    INotificationRepository notifications,
    ISmsSender sms,
    IEmailSender email,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IBackgroundJob
{
    private const int BatchSize = 50;
    private const int MaxErrorLength = 500;
    private static readonly TimeSpan SendLease = TimeSpan.FromMinutes(5);

    public async Task RunAsync(CancellationToken ct)
    {
        IReadOnlyList<Notification> batch;
        do
        {
            batch = await ClaimAsync(ct);
            foreach (var notification in batch)
            {
                await SendAsync(notification, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }
        }
        while (batch.Count == BatchSize);
    }

    private async Task<IReadOnlyList<Notification>> ClaimAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var batch = await notifications.ClaimDueAsync(now, BatchSize, ct);
        foreach (var notification in batch)
        {
            notification.Status = NotificationStatus.Sending;
            notification.Attempts++;
            notification.NextAttemptAt = now + SendLease;
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return batch;
    }

    private async Task SendAsync(Notification notification, CancellationToken ct)
    {
        var enabled = notification.Channel == MessageChannel.Email ? email.IsEnabled : sms.IsEnabled;
        if (!enabled || notification.Body is null)
        {
            notification.Status = NotificationStatus.Failed;
            notification.LastError = enabled ? "body_purged" : "channel_disabled";
            return;
        }

        try
        {
            if (notification.Channel == MessageChannel.Email)
            {
                await email.SendAsync(notification.Recipient, notification.Subject ?? "", notification.Body, ct);
            }
            else
            {
                await sms.SendAsync(notification.Recipient, notification.Body, ct);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var now = time.GetUtcNow();
            notification.LastError = Scrub(ex.Message, notification.Recipient);
            if (notification.Attempts > PlatformPolicy.NotificationBackoff.Count)
            {
                notification.Status = NotificationStatus.Failed;
            }
            else
            {
                notification.Status = NotificationStatus.Pending;
                notification.NextAttemptAt = now + PlatformPolicy.NotificationBackoff[notification.Attempts - 1];
            }

            return;
        }

        notification.Status = NotificationStatus.Sent;
        notification.SentAt = time.GetUtcNow();
        notification.LastError = null;
    }

    // Provider errors often echo the destination back; the stored error must not become a second copy of it.
    private static string Scrub(string message, string recipient)
    {
        var scrubbed = message.Replace(recipient, "[recipient]", StringComparison.OrdinalIgnoreCase);
        if (recipient.StartsWith('+'))
        {
            scrubbed = scrubbed.Replace(recipient[1..], "[recipient]", StringComparison.Ordinal);
        }

        scrubbed = PhoneLike().Replace(EmailLike().Replace(scrubbed, "[email]"), "[phone]");
        return scrubbed.Length <= MaxErrorLength ? scrubbed : scrubbed[..MaxErrorLength];
    }

    [GeneratedRegex(@"[^\s@<>""']+@[^\s@<>""']+")]
    private static partial Regex EmailLike();

    [GeneratedRegex(@"\+?\d[\d\s\-()]{7,}\d")]
    private static partial Regex PhoneLike();
}

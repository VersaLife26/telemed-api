using TeleMed.Application.Abstractions;
using TeleMed.Application.Auth;

namespace TeleMed.Application.Jobs;

// Strips personal data from accounts deleted more than ErasureGracePeriod ago. Appointments, notes, prescriptions and
// payments keep their own snapshots and stay, so clinical and financial records survive the account.
public sealed class UserErasureJob(
    IUserAccounts accounts,
    SessionIssuer sessions,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IBackgroundJob
{
    public const string ErasedName = "Deleted user";
    private const int BatchSize = 100;

    public async Task RunAsync(CancellationToken ct)
    {
        IReadOnlyList<Domain.Entities.User> batch;
        do
        {
            var now = time.GetUtcNow();
            batch = await accounts.ListDueForErasureAsync(now, BatchSize, ct);
            var photos = new List<string>();
            foreach (var user in batch)
            {
                if (user.PhotoStorageKey is { } photo)
                {
                    photos.Add(photo);
                }

                user.FullName = ErasedName;
                user.Email = null;
                user.NormalizedEmail = null;
                user.EmailConfirmed = false;
                user.PhoneNumber = null;
                user.PhoneNumberConfirmed = false;
                user.PasswordHash = null;
                user.Address = null;
                user.DateOfBirth = null;
                user.Sex = null;
                user.Allergies = null;
                user.PhotoStorageKey = null;
                user.NationalIdEncrypted = null;
                user.IsSriLankanCitizen = false;
                user.AnonymizedAt = now;
                await accounts.RemoveLoginsAsync(user, ct);
                await sessions.RevokeAllAsync(user, ct);
            }

            await unitOfWork.SaveChangesAsync(ct);
            foreach (var photo in photos)
            {
                await storage.DeleteAsync(photo, ct);
            }
        }
        while (batch.Count == BatchSize);
    }
}

using FluentValidation;
using FluentValidation.Results;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Auth;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Users;

public sealed class MeService(
    ICurrentActor actor,
    IUserAccounts accounts,
    SessionIssuer sessions,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public async Task<MeDto> GetAsync(CancellationToken ct) => (await LoadAsync(ct)).ToMeDto(storage);

    public async Task<MeDto> UpdateAsync(UpdateMeRequest request, CancellationToken ct)
    {
        var user = await LoadAsync(ct);
        if (request.Version != user.ConcurrencyStamp)
        {
            throw new ConflictException("concurrency_conflict", "Your profile was changed elsewhere. Reload and try again.");
        }

        user.FullName = request.FullName.Trim();
        user.Address = request.Address;
        user.DateOfBirth = request.DateOfBirth;
        user.Sex = request.Sex;
        user.Allergies = request.Allergies;
        user.Language = request.Language;
        await accounts.UpdateAsync(user);
        await unitOfWork.SaveChangesAsync(ct);
        return user.ToMeDto(storage);
    }

    public async Task<AuthResponse> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await LoadAsync(ct);
        await accounts.SetPasswordAsync(user, request.CurrentPassword, request.NewPassword);
        await sessions.RevokeAllAsync(user, ct);
        var session = sessions.Issue(user);
        await unitOfWork.SaveChangesAsync(ct);
        return session.Response;
    }

    public async Task DeleteAsync(CancellationToken ct)
    {
        var user = await LoadAsync(ct);
        user.Status = UserStatus.Deleted;
        user.ErasureDueAt = time.GetUtcNow() + PlatformPolicy.ErasureGracePeriod;
        await accounts.UpdateAsync(user);
        await sessions.RevokeAllAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<MeDto> SetPhotoAsync(Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length > PlatformPolicy.ProfilePhotoMaxBytes)
        {
            throw Invalid($"The photo must be at most {PlatformPolicy.ProfilePhotoMaxBytes / (1024 * 1024)} MB.");
        }

        var contentType = FileSignature.DetectContentType(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, FileSignature.HeaderLength)));
        if (contentType is null || !FileSignature.Images.Contains(contentType))
        {
            throw Invalid("Only JPEG, PNG or WebP images are allowed.");
        }

        var user = await LoadAsync(ct);
        var key = $"users/{user.Id}/photo-{Guid.CreateVersion7()}{FileSignature.Extension(contentType)}";
        buffer.Position = 0;
        await storage.SaveAsync(key, buffer, ct);

        var previous = user.PhotoStorageKey;
        user.PhotoStorageKey = key;
        await accounts.UpdateAsync(user);
        await unitOfWork.SaveChangesAsync(ct);
        if (previous is not null)
        {
            await storage.DeleteAsync(previous, ct);
        }

        return user.ToMeDto(storage);
    }

    public async Task<PhotoUrlDto> GetPhotoUrlAsync(CancellationToken ct)
    {
        var user = await LoadAsync(ct);
        var key = user.PhotoStorageKey ?? throw new NotFoundException("No profile photo.");
        return new PhotoUrlDto(storage.CreateSignedUrl(key, ProfilePhoto.ContentType(key)));
    }

    public async Task DeletePhotoAsync(CancellationToken ct)
    {
        var user = await LoadAsync(ct);
        if (user.PhotoStorageKey is not { } previous)
        {
            return;
        }

        user.PhotoStorageKey = null;
        await accounts.UpdateAsync(user);
        await unitOfWork.SaveChangesAsync(ct);
        await storage.DeleteAsync(previous, ct);
    }

    private async Task<User> LoadAsync(CancellationToken ct) =>
        await accounts.FindByIdAsync(actor.RequireUserId(), ct) ?? throw new NotFoundException("User not found.");

    private static ValidationException Invalid(string message) => new([new ValidationFailure("file", message)]);
}

using System.Security.Cryptography;
using System.Text;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Auth;

public sealed class AuthService(
    IUserAccounts accounts,
    IOtpChallengeRepository challenges,
    IRefreshTokenRepository refreshTokens,
    IOtpCodes codes,
    ISmsSender sms,
    IEmailSender email,
    IGoogleTokenValidator google,
    SessionIssuer sessions,
    ICurrentActor actor,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public const string GoogleProvider = "Google";

    public async Task<OtpSentResponse> SendOtpAsync(OtpSendRequest request, CancellationToken ct)
    {
        var destination = ParseDestination(request.Phone, request.Email);
        var channelEnabled = destination.Channel == MessageChannel.Sms ? sms.IsEnabled : email.IsEnabled;
        if (!channelEnabled)
        {
            throw new ServiceUnavailableException($"Sign-in codes cannot be sent by {destination.Channel.ToString().ToLowerInvariant()} right now.");
        }

        var now = time.GetUtcNow();
        if (await challenges.CountCreatedSinceAsync(destination.Address, now - PlatformPolicy.OtpSendWindow, ct) >= PlatformPolicy.OtpSendLimit)
        {
            throw new TooManyRequestsException("Too many codes requested. Try again later.");
        }

        var code = codes.NewCode();
        challenges.Add(new OtpChallenge
        {
            Destination = destination.Address,
            Channel = destination.Channel,
            CodeHash = codes.Hash(destination.Address, code),
            ExpiresAt = now + PlatformPolicy.OtpTtl,
        });
        await unitOfWork.SaveChangesAsync(ct);

        var language = (await FindByDestinationAsync(destination, ct))?.Language ?? request.Language ?? Language.En;
        var body = OtpMessages.Body(language, code);
        if (destination.Channel == MessageChannel.Sms)
        {
            await sms.SendAsync(destination.Address, body, ct);
        }
        else
        {
            await email.SendAsync(destination.Address, OtpMessages.Subject(language), body, ct);
        }

        return new OtpSentResponse(destination.Channel, (int)PlatformPolicy.OtpTtl.TotalSeconds);
    }

    public async Task<AuthResponse> VerifyOtpAsync(OtpVerifyRequest request, CancellationToken ct)
    {
        var destination = ParseDestination(request.Phone, request.Email);
        var now = time.GetUtcNow();

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var challenge = await challenges.FindLatestActiveForUpdateAsync(destination.Address, now, ct);
        if (challenge is null || challenge.Attempts >= PlatformPolicy.OtpMaxAttempts)
        {
            throw InvalidOtp();
        }

        challenge.Attempts++;
        var expected = Encoding.ASCII.GetBytes(challenge.CodeHash);
        var actual = Encoding.ASCII.GetBytes(codes.Hash(destination.Address, request.Code));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            throw InvalidOtp();
        }

        challenge.ConsumedAt = now;
        var user = await FindByDestinationAsync(destination, ct);
        if (user is null)
        {
            user = new User { Role = UserRole.Patient, Language = request.Language ?? Language.En };
            if (destination.Channel == MessageChannel.Sms)
            {
                user.PhoneNumber = destination.Address;
                user.PhoneNumberConfirmed = true;
            }
            else
            {
                user.Email = destination.Address;
                user.EmailConfirmed = true;
            }

            await accounts.CreateAsync(user, password: null);
        }
        else if (user.Status == UserStatus.Active)
        {
            if (destination.Channel == MessageChannel.Sms && !user.PhoneNumberConfirmed)
            {
                user.PhoneNumberConfirmed = true;
                await accounts.UpdateAsync(user);
            }
            else if (destination.Channel == MessageChannel.Email && !user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await accounts.UpdateAsync(user);
            }
        }

        if (user.Status != UserStatus.Active)
        {
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            EnsureActive(user);
        }

        var session = sessions.Issue(user);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return session.Response;
    }

    public async Task<AuthResponse> RegisterEmailAsync(RegisterEmailRequest request, CancellationToken ct)
    {
        var address = Emails.Normalize(request.Email);
        if (await accounts.FindByEmailAsync(address, ct) is not null)
        {
            throw new ConflictException("email_taken", "An account with this email already exists.");
        }

        var user = new User
        {
            Role = UserRole.Patient,
            Email = address,
            FullName = request.FullName.Trim(),
            Language = request.Language ?? Language.En,
        };
        await accounts.CreateAsync(user, request.Password);
        var session = sessions.Issue(user);
        await unitOfWork.SaveChangesAsync(ct);
        return session.Response;
    }

    public async Task<AuthResponse> LoginEmailAsync(LoginEmailRequest request, CancellationToken ct)
    {
        var user = await accounts.FindByEmailAsync(Emails.Normalize(request.Email), ct);
        var check = await accounts.CheckPasswordAsync(user, request.Password);
        if (user is not null)
        {
            await unitOfWork.SaveChangesAsync(ct);
        }

        switch (check)
        {
            case PasswordCheck.LockedOut:
                throw new UnauthorizedException("locked_out", "Too many failed sign-in attempts. Try again later.");
            case PasswordCheck.Failed:
                throw new UnauthorizedException("invalid_credentials", "Email or password is incorrect.");
        }

        EnsureActive(user!);
        var session = sessions.Issue(user!);
        await unitOfWork.SaveChangesAsync(ct);
        return session.Response;
    }

    public async Task<AuthResponse> GoogleAsync(GoogleLoginRequest request, CancellationToken ct)
    {
        if (!google.IsEnabled)
        {
            throw new NotFoundException("Google sign-in is not enabled.");
        }

        var identity = await google.ValidateAsync(request.IdToken, ct)
            ?? throw new UnauthorizedException("invalid_google_token", "The Google sign-in could not be verified.");

        var user = await accounts.FindByLoginAsync(GoogleProvider, identity.Subject, ct);
        if (user is null)
        {
            if (!identity.EmailVerified)
            {
                throw new UnauthorizedException("google_email_unverified", "Your Google email address is not verified.");
            }

            var address = Emails.Normalize(identity.Email);
            user = await accounts.FindByEmailAsync(address, ct);
            if (user is null)
            {
                user = new User
                {
                    Role = UserRole.Patient,
                    Email = address,
                    EmailConfirmed = true,
                    FullName = string.IsNullOrWhiteSpace(identity.Name) ? address.Split('@')[0] : identity.Name.Trim(),
                };
                await accounts.CreateAsync(user, password: null);
            }
            else if (!user.EmailConfirmed)
            {
                // Whoever registered this unconfirmed address may not own it; the verified Google owner takes the account over.
                await accounts.RemovePasswordAsync(user);
                await sessions.RevokeAllAsync(user, ct);
                user.EmailConfirmed = true;
                await accounts.UpdateAsync(user);
            }

            accounts.AddLogin(user, GoogleProvider, identity.Subject);
        }

        EnsureActive(user);
        var session = sessions.Issue(user);
        await unitOfWork.SaveChangesAsync(ct);
        return session.Response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var token = await refreshTokens.FindByHashForUpdateAsync(SessionIssuer.Hash(request.RefreshToken), ct)
            ?? throw new UnauthorizedException("invalid_refresh_token", "The session is invalid. Sign in again.");

        if (token.RevokedAt is { } revokedAt)
        {
            var withinGrace = token.ReplacedById is not null && now - revokedAt < PlatformPolicy.RefreshReuseGrace;
            if (!withinGrace)
            {
                foreach (var member in await refreshTokens.ListActiveByFamilyAsync(token.FamilyId, ct))
                {
                    member.RevokedAt = now;
                }

                await unitOfWork.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                throw new UnauthorizedException("refresh_token_reused", "The session is invalid. Sign in again.");
            }
        }

        if (token.ExpiresAt <= now)
        {
            throw new UnauthorizedException("refresh_token_expired", "The session has expired. Sign in again.");
        }

        var user = await accounts.FindByIdAsync(token.UserId, ct)
            ?? throw new UnauthorizedException("invalid_refresh_token", "The session is invalid. Sign in again.");
        EnsureActive(user);

        var session = sessions.Issue(user, token.FamilyId);
        token.RevokedAt ??= now;
        token.ReplacedById = session.RefreshToken.Id;
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return session.Response;
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken ct)
    {
        var token = await refreshTokens.FindByHashAsync(SessionIssuer.Hash(request.RefreshToken), ct);
        if (token is null || token.RevokedAt is not null)
        {
            return;
        }

        token.RevokedAt = time.GetUtcNow();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task LogoutAllAsync(CancellationToken ct)
    {
        var user = await accounts.FindByIdAsync(actor.RequireUserId(), ct) ?? throw new NotFoundException("User not found.");
        await sessions.RevokeAllAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static OtpDestination ParseDestination(string? phone, string? emailAddress) =>
        OtpDestination.Parse(phone, emailAddress) ?? throw new InvalidOperationException("Request was not validated.");

    private Task<User?> FindByDestinationAsync(OtpDestination destination, CancellationToken ct) =>
        destination.Channel == MessageChannel.Sms
            ? accounts.FindByPhoneAsync(destination.Address, ct)
            : accounts.FindByEmailAsync(destination.Address, ct);

    private static void EnsureActive(User user)
    {
        switch (user.Status)
        {
            case UserStatus.Suspended:
                throw new ForbiddenException("This account is suspended.", "account_suspended");
            case UserStatus.Deleted:
                throw new ForbiddenException("This account has been deleted.", "account_deleted");
        }
    }

    private static UnauthorizedException InvalidOtp() => new("otp_invalid", "The code is invalid or has expired.");
}

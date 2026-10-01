using TeleMed.Application.Abstractions;
using TeleMed.Application.Admin.Audit;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Auth;

/// <summary>
/// Sign-in and sign-out are not ordinary row changes, so they are written as
/// explicit session events rather than by the EF interceptor (refresh tokens
/// must never appear in the audit log — they carry a hash of the secret).
/// </summary>
public sealed class SessionActivity(
    IAuditLogRepository audit,
    IRequestContext request,
    TimeProvider time)
{
    public const string EntityType = "sessions";
    private const int MaxUserAgentLength = 512;

    public void LoggedIn(User user) => Record(user.Id, user.Email, "logged_in");

    public void LoggedOut(Guid userId, string? email = null) => Record(userId, email, "logged_out");

    private void Record(Guid userId, string? email, string action)
    {
        var userAgent = request.UserAgent;
        audit.Add(new AuditLog
        {
            ActorType = AuditActorType.User,
            ActorId = userId,
            ActorEmail = email,
            Action = action,
            EntityType = EntityType,
            EntityId = userId.ToString(),
            Changes = "{}",
            Ip = request.IpAddress,
            UserAgent = userAgent is { Length: > MaxUserAgentLength } ua ? ua[..MaxUserAgentLength] : userAgent,
            RequestId = request.RequestId,
            CreatedAt = time.GetUtcNow(),
        });
    }
}

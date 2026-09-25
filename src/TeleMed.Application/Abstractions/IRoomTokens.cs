using TeleMed.Domain.Enums;

namespace TeleMed.Application.Abstractions;

public sealed record RoomGrant(Guid ConsultationId, Guid UserId, UserRole Role);

// Short-lived bearer tokens for the signalling hub, bound to one consultation, user and role.
public interface IRoomTokens
{
    string Issue(RoomGrant grant);
    RoomGrant? Validate(string? token);
}

using TeleMed.Application.Common.Exceptions;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Abstractions;

public interface ICurrentActor
{
    Guid? UserId { get; }
    UserRole? Role { get; }
    AdminActor? Admin { get; }
}

public sealed record AdminActor(Guid Id, string Email, AdminRole Role);

public static class CurrentActorExtensions
{
    public static Guid RequireUserId(this ICurrentActor actor) =>
        actor.UserId ?? throw new UnauthorizedException("unauthenticated", "Sign in to continue.");

    public static AdminActor RequireAdmin(this ICurrentActor actor) =>
        actor.Admin ?? throw new UnauthorizedException("unauthenticated", "Sign in to continue.");
}

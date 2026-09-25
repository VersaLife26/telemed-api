using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TeleMed.Application.Abstractions;
using TeleMed.Infrastructure.Identity;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Api.Auth;

internal static class JwtAuthentication
{
    public const string SubjectClaim = "sub";
    public const string RoleClaim = "role";

    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((options, jwt, time) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = jwt.Value.CreateSecurityKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = SubjectClaim,
                    RoleClaimType = RoleClaim,
                    LifetimeValidator = ValidateLifetime(time),
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = RejectStaleSessionsAsync };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, CurrentActor>();
        return services;
    }

    public static LifetimeValidator ValidateLifetime(TimeProvider time) => (notBefore, expires, _, _) =>
    {
        var now = time.GetUtcNow().UtcDateTime;
        return expires is { } exp && now < exp + ClockSkew && (notBefore is null || notBefore <= now + ClockSkew);
    };

    // Suspension, deletion, logout-all and password changes rotate the security stamp or status, which must end live access tokens.
    private static async Task RejectStaleSessionsAsync(TokenValidatedContext context)
    {
        var principal = context.Principal!;
        var sessions = context.HttpContext.RequestServices.GetRequiredService<SessionValidator>();
        var valid = Guid.TryParse(principal.FindFirstValue(SubjectClaim), out var userId)
            && await sessions.IsValidAsync(userId, principal.FindFirstValue(JwtTokenIssuer.SecurityStampClaim), context.HttpContext.RequestAborted);
        if (!valid)
        {
            context.Fail("The session is no longer valid.");
        }
    }
}

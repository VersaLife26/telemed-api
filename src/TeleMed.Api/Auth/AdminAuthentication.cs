using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Permissions;
using TeleMed.Domain.Enums;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Api.Auth;

internal static class AdminAuthentication
{
    public const string Scheme = "Admin";
    public const string CloudflareScheme = "AdminCloudflareAccess";
    public const string LocalScheme = "AdminLocalJwt";
    public const string AnyAdminPolicy = "admin";
    public const string PathPrefix = "/api/v1/admin";
    public const string CloudflareHttpClient = "cloudflare-access";

    public const string EmailClaim = "email";
    public const string AdminIdClaim = "admin_id";
    public const string AdminRoleClaim = "admin_role";

    private const string IdentityType = "admin";
    public const string CloudflareHeader = "Cf-Access-Jwt-Assertion";
    private const string CloudflareCookie = "CF_Authorization";

    public static string PolicyFor(AdminPermission permission) => $"admin:{permission}";

    public static IServiceCollection AddAdminAuthentication(this IServiceCollection services)
    {
        services.AddHttpClient(CloudflareHttpClient);
        services.AddAuthentication()
            .AddPolicyScheme(Scheme, Scheme, o => o.ForwardDefaultSelector = SelectScheme)
            .AddJwtBearer(CloudflareScheme)
            .AddJwtBearer(LocalScheme);

        services.AddOptions<JwtBearerOptions>(CloudflareScheme)
            .Configure<IOptions<AdminAuthOptions>, TimeProvider, IHttpClientFactory>((options, admin, time, http) =>
            {
                var cf = admin.Value.CloudflareAccess;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = Parameters(time, cf.Issuer, cf.Audience, SecurityAlgorithms.RsaSha256);
                if (cf.Enabled)
                {
                    options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                        cf.CertsUrl,
                        new CloudflareAccessKeysRetriever(cf.Issuer),
                        new HttpDocumentRetriever(http.CreateClient(CloudflareHttpClient)) { RequireHttps = true });
                }

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = cf.Enabled ? CloudflareToken(context.Request) : null;
                        if (token is null)
                        {
                            context.NoResult();
                        }
                        else
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = ResolveAdminAsync,
                };
            });

        services.AddOptions<JwtBearerOptions>(LocalScheme)
            .Configure<IOptions<AdminAuthOptions>, TimeProvider>((options, admin, time) =>
            {
                var local = admin.Value.LocalJwt;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = Parameters(time, LocalAdminJwtOptions.Issuer, LocalAdminJwtOptions.Audience, SecurityAlgorithms.HmacSha256);
                if (local.Enabled)
                {
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(local.KeyBytes);
                }

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (!local.Enabled)
                        {
                            context.NoResult();
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = ResolveAdminAsync,
                };
            });

        var authorization = services.AddAuthorizationBuilder()
            .AddPolicy(AnyAdminPolicy, p => p.AddAuthenticationSchemes(Scheme).RequireClaim(AdminRoleClaim));
        foreach (var permission in Enum.GetValues<AdminPermission>())
        {
            authorization.AddPolicy(PolicyFor(permission), p => p
                .AddAuthenticationSchemes(Scheme)
                .RequireAssertion(ctx => Enum.TryParse<AdminRole>(ctx.User.FindFirstValue(AdminRoleClaim), out var role)
                    && PermissionMatrix.Allows(role, permission)));
        }

        return services;
    }

    private static string SelectScheme(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AdminAuthOptions>>().Value;
        if (options.CloudflareAccess.Enabled && CloudflareToken(context.Request) is not null)
        {
            return CloudflareScheme;
        }

        return options.LocalJwt.Enabled ? LocalScheme : CloudflareScheme;
    }

    private static string? CloudflareToken(HttpRequest request)
    {
        var header = request.Headers[CloudflareHeader].ToString();
        if (header.Length > 0)
        {
            return header;
        }

        return request.Cookies.TryGetValue(CloudflareCookie, out var cookie) && cookie.Length > 0 ? cookie : null;
    }

    private static TokenValidationParameters Parameters(TimeProvider time, string issuer, string audience, string algorithm) => new()
    {
        ValidIssuer = issuer,
        ValidAudience = audience,
        ValidAlgorithms = [algorithm],
        LifetimeValidator = JwtAuthentication.ValidateLifetime(time),
    };

    // The token only proves an email; the admin_users row decides whether it is an admin and with which role.
    // Unknown or inactive emails stay authenticated without a role, so admin policies answer 403 rather than 401.
    private static async Task ResolveAdminAsync(TokenValidatedContext context)
    {
        var identity = new ClaimsIdentity(IdentityType, EmailClaim, AdminRoleClaim);
        var email = context.Principal!.FindFirstValue(EmailClaim);
        if (!string.IsNullOrWhiteSpace(email))
        {
            var directory = context.HttpContext.RequestServices.GetRequiredService<IAdminDirectory>();
            var admin = await directory.FindActiveAsync(email, context.HttpContext.RequestAborted);
            identity.AddClaim(new Claim(EmailClaim, admin?.Email ?? email));
            if (admin is not null)
            {
                identity.AddClaim(new Claim(AdminIdClaim, admin.Id.ToString()));
                identity.AddClaim(new Claim(AdminRoleClaim, admin.Role.ToString()));
            }
        }

        context.Principal = new ClaimsPrincipal(identity);
    }
}

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace TeleMed.Api;

public sealed class CorsSettings
{
    public const string Section = "Cors";

    public string[] Origins { get; set; } = [];
    public string[] AdminOrigins { get; set; } = [];
}

internal static class CorsSetup
{
    public const string AdminPolicy = "admin";

    public static IServiceCollection AddTeleMedCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CorsSettings>()
            .Bind(configuration.GetSection(CorsSettings.Section))
            .Validate(o => o.Origins.Concat(o.AdminOrigins).All(IsOrigin),
                "Cors:Origins and Cors:AdminOrigins entries must be origins like https://example.com (scheme and host, no path).")
            .ValidateOnStart();

        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<CorsSettings>>((cors, settings) =>
        {
            cors.AddDefaultPolicy(p => p.WithOrigins(settings.Value.Origins).AllowAnyHeader().AllowAnyMethod());
            // Cloudflare Access authenticates the admin console with a cookie, so admin CORS must allow credentials.
            cors.AddPolicy(AdminPolicy, p => p.WithOrigins(settings.Value.AdminOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
        });
        return services;
    }

    private static bool IsOrigin(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "https" or "http"
        && value == uri.GetLeftPart(UriPartial.Authority);
}

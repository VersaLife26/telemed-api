using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace TeleMed.Api.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    public FixedWindowPolicy OtpSend { get; set; } = new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(10) };
    public FixedWindowPolicy OtpVerify { get; set; } = new() { PermitLimit = 20, Window = TimeSpan.FromMinutes(10) };
    public FixedWindowPolicy Login { get; set; } = new() { PermitLimit = 20, Window = TimeSpan.FromMinutes(10) };
    public FixedWindowPolicy DoctorApply { get; set; } = new() { PermitLimit = 5, Window = TimeSpan.FromHours(1) };
    public FixedWindowPolicy DocumentUpload { get; set; } = new() { PermitLimit = 30, Window = TimeSpan.FromMinutes(10) };
    public FixedWindowPolicy Eligibility { get; set; } = new() { PermitLimit = 30, Window = TimeSpan.FromMinutes(10) };
    public FixedWindowPolicy Slots { get; set; } = new() { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) };
    public FixedWindowPolicy PrescriptionVerify { get; set; } = new() { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) };
}

public sealed class FixedWindowPolicy
{
    public int PermitLimit { get; set; }
    public TimeSpan Window { get; set; }
}

internal static class RateLimitingSetup
{
    public const string OtpSend = "otp-send";
    public const string OtpVerify = "otp-verify";
    public const string Login = "login";
    public const string DoctorApply = "doctor-apply";
    public const string DocumentUpload = "document-upload";
    public const string Eligibility = "eligibility";
    public const string Slots = "slots";
    public const string PrescriptionVerify = "prescription-verify";

    public static IServiceCollection AddClientIpRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.Section))
            .Validate(
                o => new[] { o.OtpSend, o.OtpVerify, o.Login, o.DoctorApply, o.DocumentUpload, o.Eligibility, o.Slots, o.PrescriptionVerify }.All(p => p.PermitLimit > 0 && p.Window > TimeSpan.Zero),
                "RateLimiting policies need a positive PermitLimit and Window.")
            .ValidateOnStart();

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(OtpSend, http => ByClientIp(http, x => x.OtpSend));
            o.AddPolicy(OtpVerify, http => ByClientIp(http, x => x.OtpVerify));
            o.AddPolicy(Login, http => ByClientIp(http, x => x.Login));
            o.AddPolicy(DoctorApply, http => ByClientIp(http, x => x.DoctorApply));
            o.AddPolicy(DocumentUpload, http => ByClientIp(http, x => x.DocumentUpload));
            o.AddPolicy(Eligibility, http => ByClientIp(http, x => x.Eligibility));
            o.AddPolicy(Slots, http => ByClientIp(http, x => x.Slots));
            o.AddPolicy(PrescriptionVerify, http => ByClientIp(http, x => x.PrescriptionVerify));
        });
        return services;
    }

    private static RateLimitPartition<string> ByClientIp(HttpContext http, Func<RateLimitingOptions, FixedWindowPolicy> select)
    {
        var policy = select(http.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value);
        return RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = policy.PermitLimit, Window = policy.Window, QueueLimit = 0 });
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TeleMed.Infrastructure.Options;

internal sealed class TestSwitchReporter(IConfiguration configuration, ILogger<TestSwitchReporter> logger) : IHostedService
{
    private static readonly (string Name, Func<IConfiguration, bool> IsOn)[] Switches =
    [
        ("Sms:Provider=Capture", c => IsCapture(c["Sms:Provider"])),
        ("Email:Provider=Capture", c => IsCapture(c["Email:Provider"])),
        ("Otp:FixedCode", c => !string.IsNullOrEmpty(c["Otp:FixedCode"])),
        ("Testing:CaptureInbox:Enabled", c => c.GetValue<bool>("Testing:CaptureInbox:Enabled")),
        ("Testing:InstantMeetings:Enabled", c => c.GetValue<bool>("Testing:InstantMeetings:Enabled")),
        ("AdminAuth:LocalJwt:Enabled", c => c.GetValue<bool>("AdminAuth:LocalJwt:Enabled")),
        ("AdminAuth:AllowAnyIp", c => c.GetValue<bool>("AdminAuth:AllowAnyIp")),
        ("Payments:Mock:Enabled", c => c.GetValue<bool>("Payments:Mock:Enabled")),
        ("Payments:Mock:AutoSucceed", c => c.GetValue<bool>("Payments:Mock:AutoSucceed")),
    ];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var enabled = Switches.Where(s => s.IsOn(configuration)).Select(s => s.Name).ToArray();
        if (enabled.Length > 0)
        {
            logger.LogWarning(
                "!!! TEST SWITCHES ENABLED — do not run this configuration in production: {Switches} !!!",
                string.Join(", ", enabled));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool IsCapture(string? provider) => string.Equals(provider, "Capture", StringComparison.OrdinalIgnoreCase);
}

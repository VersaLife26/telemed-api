using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TeleMed.Application.Abstractions;

namespace TeleMed.Api.IntegrationTests.Infrastructure;

public sealed class TeleMedApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string TestSecret = "integration-test-shared-secret";
    public const string AdminSigningKey = "integration-test-admin-jwt-signing-key-0123";
    public const string AdminOrigin = "https://admin.telemed.test";
    public const string BankDataKey = "aW50ZWdyYXRpb24tdGVzdC1iYW5rLWtleS0zMmJ5dGU=";
    public const string PayHereInternationalMerchantId = "9988776";
    public const string PayHereInternationalMerchantSecret = "aW50ZXJuYXRpb25hbC1tZXJjaGFudC1zZWNyZXQ=";
    public const string PayHereMerchantId = "1221149";
    public const string PrescriptionHmacKey = "integration-test-prescription-hmac-key-0123";
    public const string PayHereMerchantSecret = "MzQ1Njc4OTAxMjM0NTY3ODkwMTIzNDU2Nzg5MA==";

    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "telemed-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = true;
            o.ValidateOnBuild = true;
        });
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = connectionString,
            ["Database:MigrateOnStartup"] = "true",
            ["Auth:Jwt:SigningKey"] = "integration-test-jwt-signing-key-0123456789",
            ["Otp:HmacKey"] = "integration-test-otp-hmac-key-0123456789",
            ["Sms:Provider"] = "Capture",
            ["Email:Provider"] = "Capture",
            ["Storage:RootPath"] = StorageRoot,
            ["Storage:SigningKey"] = "integration-test-storage-signing-key-0123",
            ["Testing:SharedSecret"] = TestSecret,
            ["Testing:CaptureInbox:Enabled"] = "true",
            ["Testing:InstantMeetings:Enabled"] = "true",
            ["Video:RoomTokenKey"] = "integration-test-room-token-key-0123456789",
            ["AppLinks:PatientAppUrl"] = "https://app.telemed.test",
            ["AppLinks:DoctorAppUrl"] = "https://doctor.telemed.test",
            ["Prescriptions:HmacKey"] = PrescriptionHmacKey,
            ["Prescriptions:VerifyBaseUrl"] = "https://verify.telemed.test",
            ["RateLimiting:OtpSend:PermitLimit"] = "10000",
            ["RateLimiting:OtpVerify:PermitLimit"] = "10000",
            ["RateLimiting:Login:PermitLimit"] = "10000",
            ["RateLimiting:DoctorApply:PermitLimit"] = "10000",
            ["RateLimiting:DocumentUpload:PermitLimit"] = "10000",
            ["RateLimiting:Eligibility:PermitLimit"] = "10000",
            ["RateLimiting:Slots:PermitLimit"] = "10000",
            ["RateLimiting:PrescriptionVerify:PermitLimit"] = "10000",
            ["Crypto:BankDataKey"] = BankDataKey,
            ["Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command"] = "Warning",
            ["AdminAuth:CloudflareAccess:Enabled"] = "false",
            ["AdminAuth:LocalJwt:Enabled"] = "true",
            ["AdminAuth:LocalJwt:SigningKey"] = AdminSigningKey,
            ["AdminAuth:AllowAnyIp"] = "true",
            ["Cors:AdminOrigins:0"] = AdminOrigin,
            ["Payments:Mock:Enabled"] = "true",
            ["Payments:PayHere:Enabled"] = "true",
            ["Payments:PayHere:MerchantId"] = PayHereMerchantId,
            ["Payments:PayHere:MerchantSecret"] = PayHereMerchantSecret,
            ["Payments:PayHere:AppId"] = "test-app",
            ["Payments:PayHere:AppSecret"] = "test-app-secret",
            ["Payments:PayHere:BaseUrl"] = "https://sandbox.payhere.lk",
            ["Payments:PayHere:NotifyUrl"] = "https://api.telemed.test/api/v1/webhooks/payhere",
            ["Payments:PayHere:ReturnUrl"] = "https://app.telemed.test/appointments",
            ["Payments:PayHere:CancelUrl"] = "https://app.telemed.test/appointments",
            ["Payments:PayHereInternational:Enabled"] = "true",
            ["Payments:PayHereInternational:MerchantId"] = PayHereInternationalMerchantId,
            ["Payments:PayHereInternational:MerchantSecret"] = PayHereInternationalMerchantSecret,
            ["Payments:PayHereInternational:AppId"] = "test-app-intl",
            ["Payments:PayHereInternational:AppSecret"] = "test-app-secret-intl",
            ["Payments:PayHereInternational:BaseUrl"] = "https://sandbox.payhere.lk",
            ["Payments:PayHereInternational:NotifyUrl"] = "https://api.telemed.test/api/v1/webhooks/payhere",
            ["Payments:PayHereInternational:ReturnUrl"] = "https://app.telemed.test/appointments",
            ["Payments:PayHereInternational:CancelUrl"] = "https://app.telemed.test/appointments",
            ["Jobs:ExpireUnpaidBookings:Enabled"] = "false",
            ["Jobs:PaymentSettlement:Enabled"] = "false",
            ["Jobs:ExpireRescheduleRequests:Enabled"] = "false",
            ["Jobs:NotificationDispatcher:Enabled"] = "false",
            ["Jobs:Reminders:Enabled"] = "false",
            ["Jobs:Housekeeping:Enabled"] = "false",
            ["Jobs:ConsultationSweep:Enabled"] = "false",
            ["Jobs:AutoCompleteAppointments:Enabled"] = "false",
            ["Jobs:DailyPayouts:Enabled"] = "false",
            ["Jobs:UserErasure:Enabled"] = "false",
        }));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<IGoogleTokenValidator, FakeGoogleTokenValidator>();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }
    }
}

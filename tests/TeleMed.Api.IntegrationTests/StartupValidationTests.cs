using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TeleMed.Api.IntegrationTests.Infrastructure;

namespace TeleMed.Api.IntegrationTests;

public class StartupValidationTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Theory]
    [InlineData("Sms:Provider", "Dialog", "Sms:Dialog")]
    [InlineData("Email:Provider", "Smtp", "Email:Smtp:Host")]
    [InlineData("Auth:Jwt:SigningKey", "too-short", "Auth:Jwt:SigningKey")]
    [InlineData("Otp:FixedCode", "12ab56", "Otp:FixedCode")]
    [InlineData("Storage:RootPath", "", "Storage:RootPath")]
    [InlineData("Crypto:BankDataKey", "c2hvcnQta2V5", "Crypto:BankDataKey")]
    [InlineData("AdminAuth:CloudflareAccess:Enabled", "true", "AdminAuth:CloudflareAccess")]
    [InlineData("AdminAuth:LocalJwt:SigningKey", "too-short", "AdminAuth:LocalJwt:SigningKey")]
    [InlineData("AdminAuth:IpAllowlist:0", "not-an-ip", "AdminAuth:IpAllowlist")]
    [InlineData("AdminAuth:BootstrapSuperAdminEmail", "not-an-email", "AdminAuth:BootstrapSuperAdminEmail")]
    [InlineData("Cors:AdminOrigins:0", "https://admin.example.com/path", "Cors:")]
    [InlineData("Payments:PayHere:AppSecret", "", "Payments:PayHere")]
    [InlineData("Payments:PayHere:NotifyUrl", "not-a-url", "Payments:PayHere")]
    [InlineData("Jobs:PaymentSettlement:Interval", "00:00:00", "Jobs:PaymentSettlement:Interval")]
    [InlineData("Video:RoomTokenKey", "too-short", "Video:RoomTokenKey")]
    [InlineData("Turn:Provider", "Cloudflare", "Turn:Provider=Cloudflare")]
    [InlineData("Turn:Provider", "Static", "Turn:Provider=Static")]
    [InlineData("AppLinks:PatientAppUrl", "not-a-url", "AppLinks:PatientAppUrl")]
    [InlineData("AppLinks:DoctorAppUrl", "not-a-url", "AppLinks:DoctorAppUrl")]
    [InlineData("Prescriptions:HmacKey", "too-short", "Prescriptions:HmacKey")]
    [InlineData("Prescriptions:VerifyBaseUrl", "", "Prescriptions:VerifyBaseUrl")]
    public async Task Misconfigured_switches_fail_fast(string key, string value, string expectedMessage)
    {
        await using var factory = Factory.WithSettings((key, value));

        var ex = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        ex.Message.ShouldContain(expectedMessage);
    }

    [Fact]
    public void Migrations_start_before_any_background_job()
    {
        var names = Factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().Select(s => s.GetType().Name).ToList();

        var migrator = names.IndexOf("DatabaseMigrator");
        migrator.ShouldBeGreaterThanOrEqualTo(0);
        var firstJob = names.FindIndex(n => n.StartsWith("PeriodicJob", StringComparison.Ordinal));
        firstJob.ShouldBeGreaterThan(migrator);
    }
}

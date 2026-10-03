using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Admin.Doctors;
using TeleMed.Application.Appointments;
using TeleMed.Application.Auth;
using TeleMed.Application.Doctors;
using TeleMed.Application.Payments;
using TeleMed.Domain.Enums;
using TeleMed.Infrastructure.Payments;

namespace TeleMed.Api.IntegrationTests;

public class InternationalPricingTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private const string Password = "correct horse battery";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sri_Lankan_citizens_pay_rupees_and_everyone_else_pays_the_multiplied_dollar_price()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.SuperAdmin);
        (await admin.PutJsonAsync("/api/v1/admin/finance/billing", new { lkrPerUsd = 300 })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var priced = await (await admin.PutJsonAsync($"/api/v1/admin/doctors/{doctor.DoctorId}/foreign-multiplier", new { multiplier = 4 }))
            .ReadAsync<AdminDoctorDto>();
        priced.ForeignMultiplier.ShouldBe(4);
        priced.ForeignFeeCents.ShouldBe(3_333);
        priced.ForeignCurrency.ShouldBe("USD");

        var listed = await (await Factory.CreateClient().GetAsync($"/api/v1/doctors/{doctor.DoctorId}", Ct)).ReadAsync<PublicDoctorDto>();
        listed.FeeCents.ShouldBe(250_000);
        listed.ForeignFeeCents.ShouldBe(3_333);

        var abroad = await RegisterAsync("US", "abroad@example.com", isSriLankanCitizen: true, nationalId: "123456789V");
        abroad.User.IsSriLankanCitizen.ShouldBeFalse();
        (await Fixture.ScalarAsync<string?>($"SELECT national_id_encrypted FROM users WHERE id = '{abroad.User.Id}'")).ShouldBeNull();

        var visitor = await RegisterAsync("LK", "visitor@example.com", isSriLankanCitizen: false, nationalId: null);
        visitor.User.IsSriLankanCitizen.ShouldBeFalse();

        var citizen = await RegisterAsync("LK", "citizen@example.com", isSriLankanCitizen: true, nationalId: "123456789V");
        citizen.User.IsSriLankanCitizen.ShouldBeTrue();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var stored = await Fixture.ScalarAsync<string>($"SELECT national_id_encrypted FROM users WHERE id = '{citizen.User.Id}'");
            scope.ServiceProvider.GetRequiredService<IBankDataCipher>().Decrypt(stored!).ShouldBe("123456789V");
        }

        var local = await BookAsync(citizen, doctor.DoctorId);
        local.FeeCents.ShouldBe(250_000);
        local.Currency.ShouldBe("LKR");

        var foreign = await BookAsync(abroad, doctor.DoctorId);
        foreign.FeeCents.ShouldBe(3_333);
        foreign.Currency.ShouldBe("USD");

        var intent = await (await Client(abroad).PostJsonAsync($"/api/v1/appointments/{foreign.Id}/payment/intent", new { provider = "payhere" }))
            .ReadAsync<PaymentIntentDto>();
        intent.Currency.ShouldBe("USD");
        intent.Checkout!.Fields["merchant_id"].ShouldBe(TeleMedApiFactory.PayHereInternationalMerchantId);
        intent.Checkout.Fields["currency"].ShouldBe("USD");
        intent.Checkout.Fields["amount"].ShouldBe("33.33");

        var orderId = intent.PaymentId.ToString("D");
        var amount = "33.33";
        var secretHash = PayHereSignature.Md5Upper(TeleMedApiFactory.PayHereInternationalMerchantSecret);
        var fields = new Dictionary<string, string>
        {
            ["merchant_id"] = TeleMedApiFactory.PayHereInternationalMerchantId,
            ["order_id"] = orderId,
            ["payment_id"] = "320027000999",
            ["payhere_amount"] = amount,
            ["payhere_currency"] = "USD",
            ["status_code"] = "2",
            ["md5sig"] = PayHereSignature.NotifyHash(TeleMedApiFactory.PayHereInternationalMerchantId, orderId, amount, "USD", "2", secretHash),
        };
        (await Factory.CreateClient().PostAsync("/api/v1/webhooks/payhere", new FormUrlEncodedContent(fields), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var domesticFields = new Dictionary<string, string>(fields)
        {
            ["merchant_id"] = TeleMedApiFactory.PayHereMerchantId,
            ["md5sig"] = PayHereSignature.NotifyHash(
                TeleMedApiFactory.PayHereMerchantId, orderId, amount, "USD", "2", PayHereSignature.Md5Upper(TeleMedApiFactory.PayHereMerchantSecret)),
        };
        (await Factory.CreateClient().PostAsync("/api/v1/webhooks/payhere", new FormUrlEncodedContent(domesticFields), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_Sri_Lankan_connection_must_answer_and_an_unset_multiplier_blocks_international_booking()
    {
        var context = Factory.CreateClient();
        context.DefaultRequestHeaders.TryAddWithoutValidation("CF-IPCountry", "LK");
        (await (await context.GetAsync("/api/v1/auth/registration-context", Ct)).ReadAsync<RegistrationContextDto>()).AskCitizenship.ShouldBeTrue();
        (await context.PostJsonAsync("/api/v1/auth/register/email", new { email = "silent@example.com", password = Password, fullName = "Silent" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await context.PostJsonAsync("/api/v1/auth/register/email",
            new { email = "noncitizen@example.com", password = Password, fullName = "Visitor", isSriLankanCitizen = false, nationalId = "123456789V" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await (await Factory.CreateClient().GetAsync("/api/v1/auth/registration-context", Ct)).ReadAsync<RegistrationContextDto>()).AskCitizenship.ShouldBeFalse();

        var doctor = await Factory.BookableDoctorAsync(new DoctorFlows.ApplicationSpec { Phone = "+94771000991", Email = "intl-doc@example.com", SlmcNumber = "99199" });
        var patient = await RegisterAsync(null, "nofee@example.com", isSriLankanCitizen: null, nationalId: null);
        (await Client(patient).PostJsonAsync("/api/v1/appointments", BookingFlows.BookingBody(doctor.DoctorId, await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3)))))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private async Task<AuthResponse> RegisterAsync(string? country, string email, bool? isSriLankanCitizen, string? nationalId)
    {
        var client = Factory.CreateClient();
        if (country is not null)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("CF-IPCountry", country);
        }

        return await (await client.PostJsonAsync("/api/v1/auth/register/email",
            new { email, password = Password, fullName = "Patient", isSriLankanCitizen, nationalId })).ReadAsync<AuthResponse>();
    }

    private HttpClient Client(AuthResponse auth)
    {
        var client = Factory.CreateClient().WithBearer(auth.AccessToken);
        return client;
    }

    private async Task<AppointmentDto> BookAsync(AuthResponse auth, Guid doctorId) =>
        await Client(auth).BookedAsync(doctorId, await Factory.FreeSlotAsync(doctorId, TimeSpan.FromHours(3)));
}

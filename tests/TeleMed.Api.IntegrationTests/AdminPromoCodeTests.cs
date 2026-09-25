using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.Finance;
using TeleMed.Application.Common;
using TeleMed.Application.Payments;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class AdminPromoCodeTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Finance_creates_lists_updates_and_deactivates_promo_codes()
    {
        var admin = await Factory.AdminClientAsync(AdminRole.Finance);

        var created = await (await admin.PostJsonAsync("/api/v1/admin/finance/promo-codes", new
        {
            code = "welcome10",
            description = "Launch offer",
            discountType = "percent",
            percentBps = 1_000,
            maxDiscountCents = 50_000,
            minAmountCents = 0,
            maxRedemptions = 100,
            maxPerUser = 1,
        })).ReadAsync<PromoCodeDto>(HttpStatusCode.Created);
        created.Code.ShouldBe("WELCOME10");
        created.IsActive.ShouldBeTrue();
        created.RedemptionCount.ShouldBe(0);

        var duplicate = await admin.PostJsonAsync("/api/v1/admin/finance/promo-codes", new { code = "WELCOME10", discountType = "fixed", amountOffCents = 100, minAmountCents = 0 });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.ProblemCodeAsync()).ShouldBe("promo_code_exists");
        (await admin.PostJsonAsync("/api/v1/admin/finance/promo-codes", new { code = "BAD", discountType = "fixed", percentBps = 10, minAmountCents = 0 }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var updated = await (await admin.PatchJsonAsync($"/api/v1/admin/finance/promo-codes/{created.Id}", new { description = "Extended", maxRedemptions = 500 }))
            .ReadAsync<PromoCodeDto>();
        updated.Description.ShouldBe("Extended");
        updated.MaxRedemptions.ShouldBe(500);
        updated.PercentBps.ShouldBe(1_000);

        var list = await (await admin.GetAsync("/api/v1/admin/finance/promo-codes?isActive=true", Ct)).ReadAsync<PagedResult<PromoCodeDto>>();
        list.Items.ShouldHaveSingleItem().Id.ShouldBe(created.Id);

        var deactivated = await (await admin.PostAsync($"/api/v1/admin/finance/promo-codes/{created.Id}/deactivate", null, Ct)).ReadAsync<PromoCodeDto>();
        deactivated.IsActive.ShouldBeFalse();

        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var booked = await patient.Client.BookedAsync(doctor.DoctorId, await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3)));
        (await patient.Client.PutJsonAsync($"/api/v1/appointments/{booked.Id}/payment/promo", new { code = "WELCOME10" })).StatusCode
            .ShouldNotBe(HttpStatusCode.OK);

        (await admin.PatchJsonAsync($"/api/v1/admin/finance/promo-codes/{created.Id}", new { isActive = true })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var applied = await (await patient.Client.PutJsonAsync($"/api/v1/appointments/{booked.Id}/payment/promo", new { code = "welcome10" }))
            .ReadAsync<OrderSummaryDto>();
        applied.DiscountCents.ShouldBe(25_000);
    }
}

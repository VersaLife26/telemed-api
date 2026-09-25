using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Common;
using TeleMed.Application.Doctors;
using static TeleMed.Api.IntegrationTests.Infrastructure.DoctorFlows;

namespace TeleMed.Api.IntegrationTests;

public class DoctorSearchTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task SeedAsync()
    {
        await Factory.ApprovedDoctorAsync(new ApplicationSpec
        {
            Phone = "+94771000001", Email = "a@example.com", SlmcNumber = "11111", FirstName = "Anura", LastName = "Bandara",
            SpecialtyCode = "cardiology", Languages = ["en", "si"], FeeCents = 400_000, ExperienceYears = 20, Bio = "Heart failure clinic.",
        });
        await Factory.ApprovedDoctorAsync(new ApplicationSpec
        {
            Phone = "+94771000002", Email = "b@example.com", SlmcNumber = "22222", FirstName = "Bhavani", LastName = "Kumar",
            SpecialtyCode = "pediatrics", Languages = ["ta", "en"], FeeCents = 150_000, ExperienceYears = 5, Bio = "Newborn and asthma care.",
        });
        await Factory.ApprovedDoctorAsync(new ApplicationSpec
        {
            Phone = "+94771000003", Email = "c@example.com", SlmcNumber = "33333", FirstName = "Chaminda", LastName = "Silva",
            SpecialtyCode = "general_practice", Languages = ["si"], FeeCents = 250_000, ExperienceYears = 12, Bio = "Diabetes and asthma follow-up.",
        });
        await Factory.SubmitApplicationAsync(new ApplicationSpec
        {
            Phone = "+94771000004", Email = "d@example.com", SlmcNumber = "44444", FirstName = "Dilan", LastName = "Pending",
        });
    }

    private async Task<PagedResult<PublicDoctorDto>> SearchAsync(string query) =>
        await (await Factory.CreateClient().GetAsync("/api/v1/doctors" + query, Ct)).ReadAsync<PagedResult<PublicDoctorDto>>();

    private async Task<string[]> NamesAsync(string query) => (await SearchAsync(query)).Items.Select(d => d.DisplayName).ToArray();

    [Fact]
    public async Task Only_active_doctors_are_listed_sorted_by_name_by_default()
    {
        await SeedAsync();

        (await NamesAsync("")).ShouldBe(["Anura Bandara", "Bhavani Kumar", "Chaminda Silva"]);
    }

    [Fact]
    public async Task Filters_narrow_the_results()
    {
        await SeedAsync();

        (await NamesAsync("?specialty=pediatrics")).ShouldBe(["Bhavani Kumar"]);
        (await NamesAsync("?language=si")).ShouldBe(["Anura Bandara", "Chaminda Silva"]);
        (await NamesAsync("?minFee=200000&maxFee=300000")).ShouldBe(["Chaminda Silva"]);
        (await NamesAsync("?q=asthma")).ShouldBe(["Bhavani Kumar", "Chaminda Silva"], ignoreOrder: true);
        (await NamesAsync("?q=cardiology")).ShouldBe(["Anura Bandara"]);
        (await NamesAsync("?q=asthma&language=si")).ShouldBe(["Chaminda Silva"]);
        (await NamesAsync("?q=nobody")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Results_sort_by_fee_or_experience_and_page()
    {
        await SeedAsync();

        (await NamesAsync("?sort=fee")).ShouldBe(["Bhavani Kumar", "Chaminda Silva", "Anura Bandara"]);
        (await NamesAsync("?sort=experience")).ShouldBe(["Anura Bandara", "Chaminda Silva", "Bhavani Kumar"]);

        var page = await SearchAsync("?sort=fee&page=2&pageSize=2");
        page.Total.ShouldBe(3);
        page.Items.Single().DisplayName.ShouldBe("Anura Bandara");
    }

    [Fact]
    public async Task Invalid_filters_are_rejected_and_unknown_doctors_are_not_found()
    {
        var client = Factory.CreateClient();

        (await client.GetAsync("/api/v1/doctors?minFee=500&maxFee=100", Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/v1/doctors?pageSize=1000", Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync($"/api/v1/doctors/{Guid.NewGuid()}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Doctor_detail_exposes_no_private_fields()
    {
        var (doctorId, _) = await Factory.ApprovedDoctorAsync();

        var body = await (await Factory.CreateClient().GetAsync($"/api/v1/doctors/{doctorId}", Ct)).Content.ReadAsStringAsync(Ct);

        body.ShouldContain("\"displayName\":\"Nimal Perera\"");
        body.ShouldNotContain("bank", Case.Insensitive);
        body.ShouldNotContain("slmc", Case.Insensitive);
        body.ShouldNotContain("+9477");
    }
}

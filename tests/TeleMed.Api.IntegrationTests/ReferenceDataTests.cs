using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Reference;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class ReferenceDataTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task Specialties_are_public_and_seeded_in_display_order()
    {
        var specialties = await (await Factory.CreateClient().GetAsync("/api/v1/specialties", TestContext.Current.CancellationToken))
            .ReadAsync<List<SpecialtyDto>>();

        specialties.Count.ShouldBe(19);
        specialties[0].Code.ShouldBe("general_practice");
        specialties.Select(s => s.DisplayOrder).ShouldBeInOrder(SortDirection.Ascending);
        specialties.ShouldAllBe(s => s.NameSi.Length > 0 && s.NameTa.Length > 0);
    }

    [Theory]
    [InlineData("/api/v1/drugs?q=pana")]
    [InlineData("/api/v1/icd10?q=dengue")]
    public async Task Clinical_reference_requires_a_doctor(string url)
    {
        var anonymous = await Factory.CreateClient().GetAsync(url, TestContext.Current.CancellationToken);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var patient = await Factory.CreateUserAsync(UserRole.Patient);
        var forbidden = await Factory.CreateClient().WithBearer(patient.AccessToken).GetAsync(url, TestContext.Current.CancellationToken);
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Drug_search_matches_brand_and_generic_prefixes()
    {
        var client = await DoctorClientAsync();

        var brand = await (await client.GetAsync("/api/v1/drugs?q=panad", TestContext.Current.CancellationToken)).ReadAsync<List<DrugDto>>();
        var generic = await (await client.GetAsync("/api/v1/drugs?q=Paracetamol", TestContext.Current.CancellationToken)).ReadAsync<List<DrugDto>>();
        var wildcard = await (await client.GetAsync("/api/v1/drugs?q=%25%25", TestContext.Current.CancellationToken)).ReadAsync<List<DrugDto>>();

        brand.ShouldContain(d => d.Name == "Panadol");
        generic.Count.ShouldBeGreaterThanOrEqualTo(2);
        wildcard.ShouldBeEmpty();
    }

    [Fact]
    public async Task Icd10_search_supports_code_prefix_and_text()
    {
        var client = await DoctorClientAsync();

        var byCode = await (await client.GetAsync("/api/v1/icd10?q=E11", TestContext.Current.CancellationToken)).ReadAsync<List<Icd10CodeDto>>();
        var byText = await (await client.GetAsync("/api/v1/icd10?q=dengue%20haem", TestContext.Current.CancellationToken)).ReadAsync<List<Icd10CodeDto>>();
        var bySynonym = await (await client.GetAsync("/api/v1/icd10?q=sugar", TestContext.Current.CancellationToken)).ReadAsync<List<Icd10CodeDto>>();
        var operators = await (await client.GetAsync("/api/v1/icd10?q=%21%26%7C%3A%2A", TestContext.Current.CancellationToken)).ReadAsync<List<Icd10CodeDto>>();

        byCode.ShouldNotBeEmpty();
        byCode.ShouldAllBe(c => c.Code.StartsWith("E11"));
        byText.ShouldNotBeEmpty();
        byText[0].Code.ShouldStartWith("A91");
        bySynonym.ShouldNotBeEmpty();
        operators.ShouldBeEmpty();
    }

    [Fact]
    public async Task Empty_query_is_a_validation_error()
    {
        var client = await DoctorClientAsync();

        var response = await client.GetAsync("/api/v1/drugs?q=", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<HttpClient> DoctorClientAsync()
    {
        var doctor = await Factory.CreateUserAsync(UserRole.Doctor);
        return Factory.CreateClient().WithBearer(doctor.AccessToken);
    }
}

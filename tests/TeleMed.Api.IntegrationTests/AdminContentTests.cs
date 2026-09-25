using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.Content;
using TeleMed.Application.Common;
using TeleMed.Application.Reference;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class AdminContentTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task Specialty_lifecycle_is_reflected_in_the_public_list()
    {
        var client = await Factory.AdminClientAsync(AdminRole.Support);

        var created = await (await client.PostJsonAsync("/api/v1/admin/specialties",
                new { code = "sports_medicine", nameEn = " Sports Medicine ", nameSi = "ක්‍රීඩා වෛද්‍ය", nameTa = "விளையாட்டு மருத்துவம்", displayOrder = 5 }))
            .ReadAsync<AdminSpecialtyDto>(HttpStatusCode.Created);
        var visible = await PublicSpecialtiesAsync();

        (await client.DeleteAsync("/api/v1/admin/specialties/sports_medicine", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var hidden = await PublicSpecialtiesAsync();
        var adminList = await (await client.GetAsync("/api/v1/admin/specialties", TestContext.Current.CancellationToken))
            .ReadAsync<List<AdminSpecialtyDto>>();

        created.NameEn.ShouldBe("Sports Medicine");
        created.IsActive.ShouldBeTrue();
        visible.ShouldContain(s => s.Code == "sports_medicine");
        hidden.ShouldNotContain(s => s.Code == "sports_medicine");
        adminList.ShouldContain(s => s.Code == "sports_medicine" && !s.IsActive);
    }

    [Fact]
    public async Task Specialty_validation_and_conflicts()
    {
        var client = await Factory.AdminClientAsync(AdminRole.Admin);

        var badCode = await client.PostJsonAsync("/api/v1/admin/specialties",
            new { code = "Bad Code", nameEn = "X", nameSi = "X", nameTa = "X", displayOrder = 1 });
        var duplicate = await client.PostJsonAsync("/api/v1/admin/specialties",
            new { code = "general_practice", nameEn = "X", nameSi = "X", nameTa = "X", displayOrder = 1 });
        var missing = await client.PutJsonAsync("/api/v1/admin/specialties/nope",
            new { nameEn = "X", nameSi = "X", nameTa = "X", displayOrder = 1 });

        badCode.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.ProblemCodeAsync()).ShouldBe("specialty_exists");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Drug_lifecycle_and_search()
    {
        var client = await Factory.AdminClientAsync(AdminRole.Ops);
        var doctor = await Factory.CreateUserAsync(UserRole.Doctor);
        var doctorClient = Factory.CreateClient().WithBearer(doctor.AccessToken);

        var created = await (await client.PostJsonAsync("/api/v1/admin/drugs", new
        {
            name = "Zyntrex",
            genericName = "Zyntramine",
            strength = "20mg",
            form = "capsule",
            manufacturer = "",
            isControlled = true,
            isGeneric = false,
        })).ReadAsync<AdminDrugDto>(HttpStatusCode.Created);
        var updated = await (await client.PutJsonAsync($"/api/v1/admin/drugs/{created.Id}", new
        {
            name = "Zyntrex",
            genericName = "Zyntramine",
            strength = "40mg",
            form = "capsule",
            isControlled = true,
            isGeneric = false,
        })).ReadAsync<AdminDrugDto>();
        var searchable = await (await doctorClient.GetAsync("/api/v1/drugs?q=zynt", TestContext.Current.CancellationToken)).ReadAsync<List<DrugDto>>();

        (await client.DeleteAsync($"/api/v1/admin/drugs/{created.Id}", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var afterDelete = await (await doctorClient.GetAsync("/api/v1/drugs?q=zynt", TestContext.Current.CancellationToken)).ReadAsync<List<DrugDto>>();
        var adminSearch = await (await client.GetAsync("/api/v1/admin/drugs?q=ZYNTRA&pageSize=5", TestContext.Current.CancellationToken))
            .ReadAsync<PagedResult<AdminDrugDto>>();

        created.Manufacturer.ShouldBeNull();
        updated.Strength.ShouldBe("40mg");
        searchable.ShouldHaveSingleItem().Id.ShouldBe(created.Id);
        afterDelete.ShouldBeEmpty();
        adminSearch.Total.ShouldBe(1);
        adminSearch.Items.ShouldHaveSingleItem().IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Drug_list_is_paged_and_validated()
    {
        var client = await Factory.AdminClientAsync(AdminRole.Admin);

        var page = await (await client.GetAsync("/api/v1/admin/drugs?page=2&pageSize=10", TestContext.Current.CancellationToken))
            .ReadAsync<PagedResult<AdminDrugDto>>();
        var invalid = await client.GetAsync("/api/v1/admin/drugs?pageSize=1000", TestContext.Current.CancellationToken);
        var badDrug = await client.PostJsonAsync("/api/v1/admin/drugs", new { name = "", genericName = "X", strength = "1", form = "tablet" });

        page.Page.ShouldBe(2);
        page.Items.Count.ShouldBe(10);
        page.Total.ShouldBeGreaterThan(20);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        badDrug.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<List<SpecialtyDto>> PublicSpecialtiesAsync() =>
        await (await Factory.CreateClient().GetAsync("/api/v1/specialties", TestContext.Current.CancellationToken)).ReadAsync<List<SpecialtyDto>>();
}

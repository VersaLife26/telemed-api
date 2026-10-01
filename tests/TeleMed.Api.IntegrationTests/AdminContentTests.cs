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

    [Fact]
    public async Task Waiting_room_item_lifecycle_is_visible_to_patients()
    {
        var admin = await Factory.AdminClientAsync(AdminRole.Support);
        var patient = Factory.CreateClient().WithBearer((await Factory.CreateUserAsync(UserRole.Patient)).AccessToken);
        var doctor = Factory.CreateClient().WithBearer((await Factory.CreateUserAsync(UserRole.Doctor)).AccessToken);

        var created = await (await admin.PostJsonAsync("/api/v1/admin/waiting-room-items", new
        {
            kind = "article",
            title = " How to prepare ",
            body = "Bring your reports.",
            displayOrder = 10,
        })).ReadAsync<AdminWaitingRoomItemDto>(HttpStatusCode.Created);
        var withImage = await (await admin.PutAsync($"/api/v1/admin/waiting-room-items/{created.Id}/image",
                DoctorFlows.File(DoctorFlows.Png, "hero.png", "image/png"), TestContext.Current.CancellationToken))
            .ReadAsync<AdminWaitingRoomItemDto>();
        var ad = await (await admin.PostJsonAsync("/api/v1/admin/waiting-room-items", new
        {
            kind = "ad",
            title = "VersaLife Health",
            body = "Care from home.",
            linkUrl = "https://versalifehealth.com",
            videoUrl = "https://example.com/ad.mp4",
            displayOrder = 20,
        })).ReadAsync<AdminWaitingRoomItemDto>(HttpStatusCode.Created);
        var withVideo = await (await admin.PutAsync($"/api/v1/admin/waiting-room-items/{ad.Id}/video",
                DoctorFlows.File(DoctorFlows.Mp4, "ad.mp4", "video/mp4"), TestContext.Current.CancellationToken))
            .ReadAsync<AdminWaitingRoomItemDto>();

        var published = await (await patient.GetAsync("/api/v1/waiting-room-content", TestContext.Current.CancellationToken))
            .ReadAsync<List<WaitingRoomItemDto>>();
        (await Factory.CreateClient().GetAsync("/api/v1/waiting-room-content", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await doctor.GetAsync("/api/v1/waiting-room-content", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var file = await Factory.CreateClient().GetAsync(withImage.ImageUrl!, TestContext.Current.CancellationToken);
        (await admin.DeleteAsync($"/api/v1/admin/waiting-room-items/{created.Id}", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var afterHide = await (await patient.GetAsync("/api/v1/waiting-room-content", TestContext.Current.CancellationToken))
            .ReadAsync<List<WaitingRoomItemDto>>();
        (await admin.DeleteAsync($"/api/v1/admin/waiting-room-items/{ad.Id}/image", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var video = await Factory.CreateClient().GetAsync(withVideo.VideoFileUrl!, TestContext.Current.CancellationToken);

        created.Title.ShouldBe("How to prepare");
        created.Kind.ShouldBe(WaitingRoomItemKind.Article);
        withImage.ImageUrl.ShouldNotBeNull();
        withVideo.VideoFileUrl.ShouldNotBeNull();
        file.StatusCode.ShouldBe(HttpStatusCode.OK);
        video.StatusCode.ShouldBe(HttpStatusCode.OK);
        published.ShouldContain(i => i.Id == created.Id && i.ImageUrl != null);
        published.ShouldContain(i => i.Id == ad.Id && i.Kind == WaitingRoomItemKind.Ad && i.VideoFileUrl != null);
        afterHide.ShouldNotContain(i => i.Id == created.Id);
        afterHide.ShouldContain(i => i.Id == ad.Id);
    }

    [Fact]
    public async Task Waiting_room_item_validation()
    {
        var client = await Factory.AdminClientAsync(AdminRole.Admin);

        var missingBody = await client.PostJsonAsync("/api/v1/admin/waiting-room-items",
            new { kind = "article", title = "Empty", displayOrder = 1 });
        var badLink = await client.PostJsonAsync("/api/v1/admin/waiting-room-items",
            new { kind = "ad", title = "Ad", linkUrl = "not-a-url", displayOrder = 1 });
        var missing = await client.PutJsonAsync($"/api/v1/admin/waiting-room-items/{Guid.CreateVersion7()}",
            new { kind = "ad", title = "Gone", displayOrder = 1 });

        missingBody.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        badLink.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<List<SpecialtyDto>> PublicSpecialtiesAsync() =>
        await (await Factory.CreateClient().GetAsync("/api/v1/specialties", TestContext.Current.CancellationToken)).ReadAsync<List<SpecialtyDto>>();
}

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Users;

namespace TeleMed.Api.IntegrationTests;

public class PhotoAndFilesTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static readonly byte[] Png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89,
    ];

    [Fact]
    public async Task Non_image_upload_is_rejected_even_with_an_image_content_type()
    {
        var client = await SignedInClientAsync();

        var response = await client.PutAsync("/api/v1/me/photo", Upload(Encoding.UTF8.GetBytes("<script>alert(1)</script>"), "image/png"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Oversized_upload_is_rejected()
    {
        var client = await SignedInClientAsync();
        var big = new byte[(5 * 1024 * 1024) + 1];
        Png.CopyTo(big, 0);

        var response = await client.PutAsync("/api/v1/me/photo", Upload(big, "image/png"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Uploaded_photo_is_served_through_an_expiring_signed_url()
    {
        var client = await SignedInClientAsync();

        var me = await (await client.PutAsync("/api/v1/me/photo", Upload(Png, "application/octet-stream"), TestContext.Current.CancellationToken))
            .ReadAsync<MeDto>();
        me.PhotoUrl.ShouldNotBeNull();

        var anonymous = Factory.CreateClient();
        var file = await anonymous.GetAsync(me.PhotoUrl, TestContext.Current.CancellationToken);
        file.StatusCode.ShouldBe(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await file.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBe(Png);

        var tampered = me.PhotoUrl[..^2] + (me.PhotoUrl[^2] == 'A' ? "B" : "A") + me.PhotoUrl[^1];
        (await anonymous.GetAsync(tampered, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        Factory.Time.Advance(TimeSpan.FromMinutes(5));
        (await anonymous.GetAsync(me.PhotoUrl, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Photo_can_be_fetched_and_deleted()
    {
        var client = await SignedInClientAsync();
        (await client.GetAsync("/api/v1/me/photo", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await client.PutAsync("/api/v1/me/photo", Upload(Png, "image/png"), TestContext.Current.CancellationToken);

        var photo = await (await client.GetAsync("/api/v1/me/photo", TestContext.Current.CancellationToken)).ReadAsync<PhotoUrlDto>();
        (await Factory.CreateClient().GetAsync(photo.Url, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.DeleteAsync("/api/v1/me/photo", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/v1/me/photo", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Factory.CreateClient().GetAsync(photo.Url, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> SignedInClientAsync()
    {
        var auth = await Factory.SignInWithPhoneAsync();
        return Factory.CreateClient().WithBearer(auth.AccessToken);
    }

    private static MultipartFormDataContent Upload(byte[] bytes, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", "photo.png" } };
    }
}

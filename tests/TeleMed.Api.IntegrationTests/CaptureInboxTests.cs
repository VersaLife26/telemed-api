using System.Net;
using Microsoft.Extensions.Options;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Testing;

namespace TeleMed.Api.IntegrationTests;

public class CaptureInboxTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private const string Url = "/api/v1/test/captured-messages";

    [Fact]
    public async Task Requires_the_shared_secret()
    {
        var missing = await Factory.CreateClient().GetAsync(Url, TestContext.Current.CancellationToken);

        var wrongClient = Factory.CreateClient();
        wrongClient.DefaultRequestHeaders.Add("X-Test-Secret", "not-the-secret");
        var wrong = await wrongClient.GetAsync(Url, TestContext.Current.CancellationToken);

        var right = await Factory.CreateTestInboxClient().GetAsync(Url, TestContext.Current.CancellationToken);

        missing.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        right.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Is_404_when_disabled_even_with_the_secret()
    {
        await using var factory = Factory.WithSettings(("Testing:CaptureInbox:Enabled", "false"));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Secret", TeleMedApiFactory.TestSecret);

        (await client.GetAsync(Url, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync(Url + "/latest", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.DeleteAsync(Url, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Startup_fails_when_enabled_without_a_shared_secret()
    {
        await using var factory = Factory.WithSettings(("Testing:SharedSecret", ""));

        var ex = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        ex.Message.ShouldContain("Testing:SharedSecret");
    }

    [Fact]
    public async Task Lists_filters_and_clears_messages()
    {
        var client = Factory.CreateClient();
        await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone = "+94771111111" });
        await client.PostJsonAsync("/api/v1/auth/otp/send", new { email = "inbox@example.com" });
        var inbox = Factory.CreateTestInboxClient();

        var all = await (await inbox.GetAsync(Url, TestContext.Current.CancellationToken)).ReadAsync<List<CapturedMessageDto>>();
        var sms = await (await inbox.GetAsync(Url + "?channel=sms", TestContext.Current.CancellationToken)).ReadAsync<List<CapturedMessageDto>>();

        all.Count.ShouldBe(2);
        sms.ShouldHaveSingleItem().Recipient.ShouldBe("+94771111111");

        (await inbox.DeleteAsync(Url, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await inbox.GetAsync(Url + "/latest", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

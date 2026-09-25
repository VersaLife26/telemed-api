using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;

namespace TeleMed.Api.IntegrationTests;

public class HealthTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_return_200(string path)
    {
        var response = await Factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        var response = await Factory.CreateClient().GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

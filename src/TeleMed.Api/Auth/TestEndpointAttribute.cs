using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Api.Auth;

public enum TestFeature
{
    CaptureInbox,
    InstantMeetings,
}

internal sealed class TestEndpointAttribute : TypeFilterAttribute
{
    public TestEndpointAttribute(TestFeature feature)
        : base(typeof(TestEndpointFilter))
    {
        Arguments = [feature];
    }
}

internal sealed class TestEndpointFilter(TestFeature feature, IOptions<TestingOptions> options) : IAuthorizationFilter
{
    public const string SecretHeader = "X-Test-Secret";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var testing = options.Value;
        var enabled = feature switch
        {
            TestFeature.CaptureInbox => testing.CaptureInbox.Enabled,
            TestFeature.InstantMeetings => testing.InstantMeetings.Enabled,
            _ => false,
        };
        if (!enabled)
        {
            context.Result = new NotFoundResult();
            return;
        }

        var provided = context.HttpContext.Request.Headers[SecretHeader].ToString();
        if (testing.SharedSecret.Length == 0 || !SecretsMatch(provided, testing.SharedSecret))
        {
            context.Result = new UnauthorizedResult();
        }
    }

    private static bool SecretsMatch(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}

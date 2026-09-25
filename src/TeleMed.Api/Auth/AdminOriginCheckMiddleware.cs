using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace TeleMed.Api.Auth;

// The Cloudflare Access cookie rides along on cross-site requests, so unsafe admin calls must prove they came from the admin console.
internal sealed class AdminOriginCheckMiddleware(RequestDelegate next, IOptions<CorsSettings> cors)
{
    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problems)
    {
        var method = context.Request.Method;
        var origin = context.Request.Headers.Origin.ToString();
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method)
            || (origin.Length > 0 && cors.Value.AdminOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            || context.Request.Headers["Sec-Fetch-Site"] == "same-origin")
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Detail = "Cross-origin admin requests are not allowed.",
                Extensions = { ["code"] = "origin_not_allowed" },
            },
        });
    }
}

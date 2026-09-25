using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Api.Auth;

internal sealed class AdminIpAllowlistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _allowAny;
    private readonly IReadOnlyList<IPNetwork> _networks;
    private readonly ILogger<AdminIpAllowlistMiddleware> _logger;

    public AdminIpAllowlistMiddleware(RequestDelegate next, IOptions<AdminAuthOptions> options, ILogger<AdminIpAllowlistMiddleware> logger)
    {
        _next = next;
        _allowAny = options.Value.AllowAnyIp;
        options.Value.TryParseAllowlist(out _networks);
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problems)
    {
        var ip = context.Connection.RemoteIpAddress;
        if (ip is { IsIPv4MappedToIPv6: true })
        {
            ip = ip.MapToIPv4();
        }

        if (_allowAny || (ip is not null && _networks.Any(n => n.Contains(ip))))
        {
            await _next(context);
            return;
        }

        _logger.LogWarning("Admin request from {Ip} to {Path} refused by the IP allowlist", ip, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Detail = "Your network is not allowed to reach the admin API.",
                Extensions = { ["code"] = "ip_not_allowed" },
            },
        });
    }
}

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Analytics;
using TeleMed.Application.Common;
using TeleMed.Application.Permissions;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin/analytics")]
[AdminAuthorize(AdminPermission.Analytics)]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminAnalyticsController(AnalyticsService analytics) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardDto> Dashboard([FromQuery] DateRangeQuery query, CancellationToken ct) => analytics.DashboardAsync(query, ct);

    [HttpGet("revenue")]
    public Task<IReadOnlyList<RevenuePointDto>> Revenue([FromQuery] RevenueQuery query, CancellationToken ct) => analytics.RevenueAsync(query, ct);

    [HttpGet("bookings")]
    public Task<IReadOnlyList<BookingsDayDto>> Bookings([FromQuery] DateRangeQuery query, CancellationToken ct) => analytics.BookingsAsync(query, ct);

    [HttpGet("top-doctors")]
    public Task<IReadOnlyList<TopDoctorDto>> TopDoctors([FromQuery] TopDoctorsQuery query, CancellationToken ct) => analytics.TopDoctorsAsync(query, ct);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Application.Analytics;
using TeleMed.Application.Common;
using TeleMed.Application.Payouts;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("doctors/me")]
[Authorize(Roles = UserRoleNames.Doctor)]
public sealed class DoctorInsightsController(DoctorInsightsService insights, PayoutService payouts) : ControllerBase
{
    [HttpGet("analytics")]
    public Task<DoctorAnalyticsDto> Analytics([FromQuery] DateRangeQuery query, CancellationToken ct) => insights.AnalyticsAsync(query, ct);

    [HttpGet("analytics/peak-hours")]
    public Task<IReadOnlyList<PeakHourDto>> PeakHours([FromQuery] DateRangeQuery query, CancellationToken ct) => insights.PeakHoursAsync(query, ct);

    [HttpGet("earnings")]
    public Task<DoctorEarningsDto> Earnings([FromQuery] DateRangeQuery query, CancellationToken ct) => insights.EarningsAsync(query, ct);

    [HttpGet("payouts")]
    public Task<PagedResult<DoctorPayoutDto>> Payouts([FromQuery] DoctorPayoutQuery query, CancellationToken ct) => payouts.ListMineAsync(query, ct);
}

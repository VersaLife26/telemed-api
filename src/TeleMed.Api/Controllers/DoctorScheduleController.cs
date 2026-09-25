using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Application.Scheduling;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("doctors/me")]
[Authorize(Roles = UserRoleNames.Doctor)]
public sealed class DoctorScheduleController(ScheduleService schedule, HolidayService holidays) : ControllerBase
{
    [HttpGet("schedule")]
    public Task<ScheduleDto> GetSchedule(CancellationToken ct) => schedule.GetMineAsync(ct);

    [HttpPut("schedule")]
    public Task<ScheduleUpdatedDto> UpdateSchedule(UpdateScheduleRequest request, CancellationToken ct) => schedule.UpdateMineAsync(request, ct);

    [HttpGet("holidays")]
    public Task<IReadOnlyList<HolidayDto>> ListHolidays([FromQuery] HolidayQuery query, CancellationToken ct) => holidays.ListMineAsync(query, ct);

    [HttpPost("holidays")]
    [ProducesResponseType<HolidayDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> AddHoliday(CreateHolidayRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await holidays.AddMineAsync(request, ct));

    [HttpDelete("holidays/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> DeleteHoliday(Guid id, CancellationToken ct)
    {
        await holidays.DeleteMineAsync(id, ct);
        return NoContent();
    }
}

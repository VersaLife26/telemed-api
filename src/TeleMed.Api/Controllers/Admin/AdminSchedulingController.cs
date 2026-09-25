using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Permissions;
using TeleMed.Application.Scheduling;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin")]
[AdminAuthorize(AdminPermission.Doctors)]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminSchedulingController(ScheduleService schedule, HolidayService holidays, SlotBlockService blocks) : ControllerBase
{
    [HttpGet("doctors/{id:guid}/schedule")]
    public Task<ScheduleDto> GetSchedule(Guid id, CancellationToken ct) => schedule.GetAsync(id, ct);

    [HttpPut("doctors/{id:guid}/schedule")]
    public Task<ScheduleUpdatedDto> UpdateSchedule(Guid id, UpdateScheduleRequest request, CancellationToken ct) =>
        schedule.UpdateAsync(id, request, ct);

    [HttpGet("doctors/{id:guid}/holidays")]
    public Task<IReadOnlyList<HolidayDto>> ListDoctorHolidays(Guid id, [FromQuery] HolidayQuery query, CancellationToken ct) =>
        holidays.ListForDoctorAsync(id, query, ct);

    [HttpPost("doctors/{id:guid}/holidays")]
    [ProducesResponseType<HolidayDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> AddDoctorHoliday(Guid id, CreateHolidayRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await holidays.AddForDoctorAsync(id, request, ct));

    [HttpGet("doctors/{id:guid}/slot-blocks")]
    public Task<IReadOnlyList<SlotBlockDto>> ListSlotBlocks(Guid id, [FromQuery] SlotBlockQuery query, CancellationToken ct) =>
        blocks.ListAsync(id, query, ct);

    [HttpPost("doctors/{id:guid}/slot-blocks")]
    [ProducesResponseType<SlotBlockDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> AddSlotBlock(Guid id, CreateSlotBlockRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await blocks.AddAsync(id, request, ct));

    [HttpDelete("slot-blocks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> DeleteSlotBlock(Guid id, CancellationToken ct)
    {
        await blocks.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("holidays")]
    public Task<IReadOnlyList<HolidayDto>> ListPlatformHolidays([FromQuery] HolidayQuery query, CancellationToken ct) =>
        holidays.ListPlatformAsync(query, ct);

    [HttpPost("holidays")]
    [ProducesResponseType<HolidayDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> AddPlatformHoliday(CreateHolidayRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await holidays.AddPlatformAsync(request, ct));

    [HttpDelete("holidays/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> DeleteHoliday(Guid id, CancellationToken ct)
    {
        await holidays.DeleteAsync(id, ct);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TeleMed.Application.Common;
using TeleMed.Application.Consultations;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("appointments/{appointmentId:guid}/consultation")]
[Authorize(Roles = $"{UserRoleNames.Patient},{UserRoleNames.Doctor}")]
public sealed class ConsultationsController(ConsultationService consultations) : ControllerBase
{
    [HttpPost("join")]
    public Task<JoinConsultationDto> Join(Guid appointmentId, CancellationToken ct) => consultations.JoinAsync(appointmentId, ct);

    [HttpGet]
    public Task<ConsultationDto> Get(Guid appointmentId, CancellationToken ct) => consultations.GetAsync(appointmentId, ct);

    [HttpGet("waiting-room")]
    public Task<WaitingRoomDto> WaitingRoom(Guid appointmentId, CancellationToken ct) => consultations.GetWaitingRoomAsync(appointmentId, ct);

    [HttpPost("admit")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<ConsultationDto> Admit(Guid appointmentId, CancellationToken ct) => consultations.AdmitAsync(appointmentId, ct);

    [HttpPost("end")]
    public Task<ConsultationDto> End(
        Guid appointmentId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] EndConsultationRequest? request,
        CancellationToken ct) =>
        consultations.EndAsync(appointmentId, request, ct);

    [HttpPost("quality")]
    public Task<QualityFeedbackDto> Quality(Guid appointmentId, QualityReportRequest request, CancellationToken ct) =>
        consultations.ReportQualityAsync(appointmentId, request, ct);

    [HttpGet("messages")]
    public Task<PagedResult<ConsultationMessageDto>> Messages(Guid appointmentId, [FromQuery] ConsultationMessageQuery query, CancellationToken ct) =>
        consultations.ListMessagesAsync(appointmentId, query, ct);

    [HttpPost("messages")]
    [ProducesResponseType<ConsultationMessageDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> PostMessage(Guid appointmentId, PostMessageRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await consultations.PostMessageAsync(appointmentId, request, ct));

    [HttpPost("ready-for-next")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<ReadyForNextDto> ReadyForNext(Guid appointmentId, CancellationToken ct) => consultations.ReadyForNextAsync(appointmentId, ct);

    [HttpGet("early-join")]
    public Task<EarlyJoinDto> EarlyJoin(Guid appointmentId, CancellationToken ct) => consultations.GetEarlyJoinAsync(appointmentId, ct);

    [HttpPost("early-join/accept")]
    [Authorize(Roles = UserRoleNames.Patient)]
    public Task<EarlyJoinDto> AcceptEarlyJoin(Guid appointmentId, CancellationToken ct) =>
        consultations.RespondEarlyJoinAsync(appointmentId, EarlyJoinResponse.Accepted, ct);

    [HttpPost("early-join/decline")]
    [Authorize(Roles = UserRoleNames.Patient)]
    public Task<EarlyJoinDto> DeclineEarlyJoin(Guid appointmentId, CancellationToken ct) =>
        consultations.RespondEarlyJoinAsync(appointmentId, EarlyJoinResponse.Declined, ct);
}

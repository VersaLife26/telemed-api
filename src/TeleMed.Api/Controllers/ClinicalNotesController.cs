using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TeleMed.Application.ClinicalNotes;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{UserRoleNames.Patient},{UserRoleNames.Doctor}")]
public sealed class ClinicalNotesController(ClinicalNoteService notes) : ControllerBase
{
    [HttpGet("clinical-notes")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<PagedResult<ClinicalNoteSummaryDto>> List([FromQuery] ClinicalNoteQuery query, CancellationToken ct) => notes.ListMineAsync(query, ct);

    [HttpGet("appointments/{appointmentId:guid}/clinical-note")]
    public Task<ClinicalNoteDto> Get(Guid appointmentId, CancellationToken ct) => notes.GetAsync(appointmentId, ct);

    [HttpPut("appointments/{appointmentId:guid}/clinical-note")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<ClinicalNoteDto> Save(Guid appointmentId, SaveClinicalNoteRequest request, CancellationToken ct) =>
        notes.SaveDraftAsync(appointmentId, request, ct);

    [HttpPost("appointments/{appointmentId:guid}/clinical-note/finalise")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<ClinicalNoteDto> Finalise(
        Guid appointmentId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] FinaliseClinicalNoteRequest? request,
        CancellationToken ct) =>
        notes.FinaliseAsync(appointmentId, request, ct);

    [HttpPost("appointments/{appointmentId:guid}/clinical-note/amend")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<ClinicalNoteDto> Amend(Guid appointmentId, AmendClinicalNoteRequest request, CancellationToken ct) =>
        notes.AmendAsync(appointmentId, request, ct);

    [HttpGet("appointments/{appointmentId:guid}/clinical-note/revisions")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<IReadOnlyList<ClinicalNoteRevisionDto>> Revisions(Guid appointmentId, CancellationToken ct) =>
        notes.ListRevisionsAsync(appointmentId, ct);
}

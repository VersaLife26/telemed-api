using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Application.Reference;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Controllers;

[ApiController]
public sealed class ReferenceController(ReferenceDataService reference) : ControllerBase
{
    [HttpGet("specialties")]
    [AllowAnonymous]
    public Task<IReadOnlyList<SpecialtyDto>> Specialties(CancellationToken ct) => reference.ListSpecialtiesAsync(ct);

    [HttpGet("drugs")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<IReadOnlyList<DrugDto>> Drugs([FromQuery] SearchQuery query, CancellationToken ct) => reference.SearchDrugsAsync(query, ct);

    [HttpGet("icd10")]
    [Authorize(Roles = UserRoleNames.Doctor)]
    public Task<IReadOnlyList<Icd10CodeDto>> Icd10([FromQuery] SearchQuery query, CancellationToken ct) => reference.SearchIcd10Async(query, ct);
}

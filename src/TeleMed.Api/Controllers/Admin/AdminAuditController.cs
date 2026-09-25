using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Api.Auth;
using TeleMed.Application.Admin.Audit;
using TeleMed.Application.Common;
using TeleMed.Application.Permissions;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin")]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminAuditController(AuditService audit) : ControllerBase
{
    [HttpGet("audit")]
    [AdminAuthorize(AdminPermission.Audit)]
    public Task<PagedResult<AuditEntryDto>> List([FromQuery] AuditQuery query, CancellationToken ct) => audit.ListAsync(query, ct);

    [HttpGet("audit.csv")]
    [AdminAuthorize(AdminPermission.AuditExport)]
    [Produces(Csv.ContentType)]
    public async Task Export([FromQuery] AuditExportQuery query, CancellationToken ct)
    {
        Response.ContentType = $"{Csv.ContentType}; charset=utf-8";
        Response.Headers.ContentDisposition = "attachment; filename=\"audit.csv\"";
        await audit.WriteCsvAsync(query, Response.Body, ct);
    }
}

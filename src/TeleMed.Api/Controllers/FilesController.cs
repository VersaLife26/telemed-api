using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Application.Abstractions;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("files")]
[AllowAnonymous]
public sealed class FilesController(IFileStorage storage) : ControllerBase
{
    [HttpGet("{token}")]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, "application/octet-stream")]
    public IActionResult Get(string token)
    {
        if (storage.OpenSigned(token) is not { } file)
        {
            return NotFound();
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(file.Content, file.ContentType);
    }
}

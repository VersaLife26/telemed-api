using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Application.Payments;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("webhooks")]
[AllowAnonymous]
public sealed class PaymentWebhooksController(PaymentService payments) : ControllerBase
{
    [HttpPost("payhere")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<OkResult> PayHere(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        await payments.HandlePayHereWebhookAsync(form.ToDictionary(f => f.Key, f => f.Value.ToString()), ct);
        return Ok();
    }
}

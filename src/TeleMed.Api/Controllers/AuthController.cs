using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeleMed.Api.RateLimiting;
using TeleMed.Application.Auth;

namespace TeleMed.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("otp/send")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.OtpSend)]
    public Task<OtpSentResponse> SendOtp(OtpSendRequest request, CancellationToken ct) => auth.SendOtpAsync(request, ct);

    [HttpPost("otp/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.OtpVerify)]
    public Task<AuthResponse> VerifyOtp(OtpVerifyRequest request, CancellationToken ct) => auth.VerifyOtpAsync(request, ct);

    [HttpPost("register/email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.Login)]
    public Task<AuthResponse> RegisterEmail(RegisterEmailRequest request, CancellationToken ct) => auth.RegisterEmailAsync(request, ct);

    [HttpPost("login/email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.Login)]
    public Task<AuthResponse> LoginEmail(LoginEmailRequest request, CancellationToken ct) => auth.LoginEmailAsync(request, ct);

    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.Login)]
    public Task<AuthResponse> Google(GoogleLoginRequest request, CancellationToken ct) => auth.GoogleAsync(request, ct);

    [HttpPost("refresh")]
    [AllowAnonymous]
    public Task<AuthResponse> Refresh(RefreshRequest request, CancellationToken ct) => auth.RefreshAsync(request, ct);

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> LogoutAll(CancellationToken ct)
    {
        await auth.LogoutAllAsync(ct);
        return NoContent();
    }

    [HttpPost("password/forgot")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.OtpSend)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(request, ct);
        return NoContent();
    }

    [HttpPost("password/reset")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.Login)]
    public Task<AuthResponse> ResetPassword(ResetPasswordRequest request, CancellationToken ct) =>
        auth.ResetPasswordAsync(request, ct);
}

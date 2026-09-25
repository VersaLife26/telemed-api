using TeleMed.Application.Users;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Auth;

public sealed record OtpSendRequest(string? Phone, string? Email, Language? Language);

public sealed record OtpSentResponse(MessageChannel Channel, int ExpiresIn);

public sealed record OtpVerifyRequest(string? Phone, string? Email, string Code, Language? Language);

public sealed record RegisterEmailRequest(string Email, string Password, string FullName, Language? Language);

public sealed record LoginEmailRequest(string Email, string Password);

public sealed record GoogleLoginRequest(string IdToken);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn, MeDto User);

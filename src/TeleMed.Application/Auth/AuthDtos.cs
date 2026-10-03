using TeleMed.Application.Users;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Auth;

public sealed record OtpSendRequest(string? Phone, string? Email, Language? Language);

public sealed record OtpSentResponse(MessageChannel Channel, int ExpiresIn);

public sealed record OtpVerifyRequest(string? Phone, string? Email, string Code, Language? Language, bool? IsSriLankanCitizen = null, string? NationalId = null);

public sealed record RegisterEmailRequest(string Email, string Password, string FullName, Language? Language, bool? IsSriLankanCitizen = null, string? NationalId = null);

public sealed record LoginEmailRequest(string Email, string Password);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record GoogleLoginRequest(string IdToken, bool? IsSriLankanCitizen = null, string? NationalId = null);

public sealed record RegistrationContextDto(string? CountryCode, bool AskCitizenship);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn, MeDto User);

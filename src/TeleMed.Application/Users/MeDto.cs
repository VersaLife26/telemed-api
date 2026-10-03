using TeleMed.Domain.Enums;

namespace TeleMed.Application.Users;

public sealed record MeDto
{
    public Guid Id { get; init; }
    public UserRole Role { get; init; }
    public string FullName { get; init; } = "";
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public Language Language { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public Sex? Sex { get; init; }
    public string? Address { get; init; }
    public string? Allergies { get; init; }
    public string? PhotoUrl { get; init; }
    public bool HasPassword { get; init; }
    public bool IsSriLankanCitizen { get; init; }
    public string Version { get; init; } = "";
}

public sealed record UpdateMeRequest(
    string FullName,
    string? Address,
    DateOnly? DateOfBirth,
    Sex? Sex,
    string? Allergies,
    Language Language,
    string Version);

public sealed record ChangePasswordRequest(string? CurrentPassword, string NewPassword);

public sealed record PhotoUrlDto(string Url);

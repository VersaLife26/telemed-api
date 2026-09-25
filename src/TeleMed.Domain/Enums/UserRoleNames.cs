namespace TeleMed.Domain.Enums;

public static class UserRoleNames
{
    public const string Patient = "patient";
    public const string Doctor = "doctor";

    public static string Of(UserRole role) => role switch
    {
        UserRole.Patient => Patient,
        UserRole.Doctor => Doctor,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public static UserRole? Parse(string? name) => name switch
    {
        Patient => UserRole.Patient,
        Doctor => UserRole.Doctor,
        _ => null,
    };
}

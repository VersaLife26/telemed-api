namespace TeleMed.Domain.Rules;

// A consultation that actually started lets its doctor read the patient's records (and file new ones) until the window after the
// latest one closes. A consultation that never ended counts from its start, so "admit and never end" is not a permanent grant.
public static class TreatingAccess
{
    public static DateTimeOffset? ExpiresAt(DateTimeOffset? startedAt, DateTimeOffset? endedAt) =>
        startedAt is null ? null : (endedAt ?? startedAt.Value) + PlatformPolicy.TreatingAccessWindow;

    public static bool Grants(DateTimeOffset? startedAt, DateTimeOffset? endedAt, DateTimeOffset now) =>
        ExpiresAt(startedAt, endedAt) is { } expiresAt && now < expiresAt;

    // Consultations last seen strictly after this instant still grant access.
    public static DateTimeOffset LastSeenCutoff(DateTimeOffset now) => now - PlatformPolicy.TreatingAccessWindow;
}

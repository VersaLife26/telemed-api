using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Rules;

public static class AppointmentTransitions
{
    public static bool IsAllowed(AppointmentStatus from, AppointmentStatus to) => (from, to) switch
    {
        (AppointmentStatus.PendingPayment, AppointmentStatus.Confirmed or AppointmentStatus.Cancelled) => true,
        (AppointmentStatus.Confirmed, AppointmentStatus.Completed or AppointmentStatus.NoShow or AppointmentStatus.Cancelled) => true,
        _ => false,
    };

    public static bool IsLive(AppointmentStatus status) => status != AppointmentStatus.Cancelled;
}

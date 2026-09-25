using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Tests;

public class AppointmentTransitionsTests
{
    [Fact]
    public void Only_the_documented_transitions_are_allowed()
    {
        var allowed = new HashSet<(AppointmentStatus, AppointmentStatus)>
        {
            (AppointmentStatus.PendingPayment, AppointmentStatus.Confirmed),
            (AppointmentStatus.PendingPayment, AppointmentStatus.Cancelled),
            (AppointmentStatus.Confirmed, AppointmentStatus.Completed),
            (AppointmentStatus.Confirmed, AppointmentStatus.NoShow),
            (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled),
        };

        foreach (var from in Enum.GetValues<AppointmentStatus>())
        {
            foreach (var to in Enum.GetValues<AppointmentStatus>())
            {
                AppointmentTransitions.IsAllowed(from, to).ShouldBe(allowed.Contains((from, to)), $"{from} -> {to}");
            }
        }
    }
}

using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Tests;

public class ConsultationTransitionsTests
{
    [Theory]
    [InlineData(ConsultationStatus.Scheduled, ConsultationStatus.Waiting)]
    [InlineData(ConsultationStatus.Scheduled, ConsultationStatus.Ended)]
    [InlineData(ConsultationStatus.Scheduled, ConsultationStatus.Abandoned)]
    [InlineData(ConsultationStatus.Waiting, ConsultationStatus.Active)]
    [InlineData(ConsultationStatus.Waiting, ConsultationStatus.Ended)]
    [InlineData(ConsultationStatus.Waiting, ConsultationStatus.Abandoned)]
    [InlineData(ConsultationStatus.Active, ConsultationStatus.Ended)]
    public void Allows_forward_moves(ConsultationStatus from, ConsultationStatus to) =>
        ConsultationTransitions.IsAllowed(from, to).ShouldBeTrue();

    [Theory]
    [InlineData(ConsultationStatus.Scheduled, ConsultationStatus.Active)]
    [InlineData(ConsultationStatus.Active, ConsultationStatus.Waiting)]
    [InlineData(ConsultationStatus.Active, ConsultationStatus.Abandoned)]
    [InlineData(ConsultationStatus.Waiting, ConsultationStatus.Scheduled)]
    [InlineData(ConsultationStatus.Ended, ConsultationStatus.Active)]
    [InlineData(ConsultationStatus.Abandoned, ConsultationStatus.Waiting)]
    [InlineData(ConsultationStatus.Waiting, ConsultationStatus.Waiting)]
    public void Rejects_everything_else(ConsultationStatus from, ConsultationStatus to) =>
        ConsultationTransitions.IsAllowed(from, to).ShouldBeFalse();

    [Fact]
    public void Terminal_states_allow_nothing()
    {
        foreach (var from in new[] { ConsultationStatus.Ended, ConsultationStatus.Abandoned })
        {
            ConsultationTransitions.IsTerminal(from).ShouldBeTrue();
            Enum.GetValues<ConsultationStatus>().ShouldAllBe(to => !ConsultationTransitions.IsAllowed(from, to));
        }

        ConsultationTransitions.IsTerminal(ConsultationStatus.Active).ShouldBeFalse();
    }

    [Fact]
    public void Duration_counts_from_the_start_or_else_the_admission()
    {
        var admitted = new DateTimeOffset(2026, 10, 1, 4, 0, 0, TimeSpan.Zero);
        var consultation = new Entities.Consultation { Status = ConsultationStatus.Active, AdmittedAt = admitted, StartedAt = admitted.AddMinutes(1) };
        consultation.End(ConsultationStatus.Ended, UserRole.Doctor, "done", admitted.AddMinutes(11));
        consultation.DurationSeconds.ShouldBe(600);

        var neverAdmitted = new Entities.Consultation { Status = ConsultationStatus.Waiting };
        neverAdmitted.End(ConsultationStatus.Abandoned, null, "stale", admitted);
        neverAdmitted.DurationSeconds.ShouldBeNull();
    }
}

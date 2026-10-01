using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Tests;

public class CancellationPolicyTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(120 * 60, 100)]
    [InlineData(120 * 60 - 1, 50)]
    [InlineData(120 * 60 + 1, 100)]
    [InlineData(0, 50)]
    [InlineData(-600, 50)]
    public void Patient_notice_decides_between_full_and_half(int secondsBefore, int expected)
    {
        CancellationPolicy.RefundPercent(CancellationActor.Patient, Start.AddSeconds(-secondsBefore), Start).ShouldBe(expected);
    }

    [Theory]
    [InlineData(CancellationActor.Doctor)]
    [InlineData(CancellationActor.Admin)]
    [InlineData(CancellationActor.System)]
    public void Anyone_but_the_patient_refunds_in_full_even_late(CancellationActor actor)
    {
        CancellationPolicy.RefundPercent(actor, Start.AddMinutes(-5), Start).ShouldBe(100);
    }

    [Fact]
    public void Patient_no_show_refunds_the_doctor_share()
    {
        CancellationPolicy.PatientNoShowRefundPercent(250_000).ShouldBe(77);
    }
}

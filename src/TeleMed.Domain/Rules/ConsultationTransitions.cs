using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Rules;

public static class ConsultationTransitions
{
    public static bool IsAllowed(ConsultationStatus from, ConsultationStatus to) => (from, to) switch
    {
        (ConsultationStatus.Scheduled, ConsultationStatus.Waiting or ConsultationStatus.Ended or ConsultationStatus.Abandoned) => true,
        (ConsultationStatus.Waiting, ConsultationStatus.Active or ConsultationStatus.Ended or ConsultationStatus.Abandoned) => true,
        (ConsultationStatus.Active, ConsultationStatus.Ended) => true,
        _ => false,
    };

    public static bool IsTerminal(ConsultationStatus status) => status is ConsultationStatus.Ended or ConsultationStatus.Abandoned;
}

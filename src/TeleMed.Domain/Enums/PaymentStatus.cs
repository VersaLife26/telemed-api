namespace TeleMed.Domain.Enums;

public enum PaymentStatus
{
    Pending,
    Authorized,
    Succeeded,
    Failed,
    Voided,
    PartiallyRefunded,
    Refunded,
}

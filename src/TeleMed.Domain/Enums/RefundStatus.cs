namespace TeleMed.Domain.Enums;

public enum RefundStatus
{
    Requested,
    Approved,
    Processing,
    Succeeded,
    Failed,
    ManualRequired,
    Rejected,
}

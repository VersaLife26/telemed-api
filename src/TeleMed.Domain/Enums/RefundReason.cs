namespace TeleMed.Domain.Enums;

public enum RefundReason
{
    PatientCancellation,
    DoctorCancellation,
    AdminCancellation,
    SystemCancellation,
    LatePayment,
    RescheduleDeclined,
    DoctorNoShow,
    AdminRequest,
    Dispute,
}

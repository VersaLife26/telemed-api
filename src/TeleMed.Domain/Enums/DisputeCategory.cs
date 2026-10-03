namespace TeleMed.Domain.Enums;

public enum DisputeCategory
{
    Refund,
    Appointment,
    Consultation,
    Prescription,
    Account,
    Technical,
}

public static class DisputeCategoryText
{
    public static string Subject(this DisputeCategory category) => category switch
    {
        DisputeCategory.Refund => "Refund / payment",
        DisputeCategory.Appointment => "Appointment / scheduling",
        DisputeCategory.Consultation => "Consultation / video call",
        DisputeCategory.Prescription => "Prescription / medical report",
        DisputeCategory.Account => "Account / login",
        DisputeCategory.Technical => "Technical issue",
        _ => category.ToString(),
    };
}

public enum DisputeOpener
{
    Admin,
    Patient,
    Doctor,
}

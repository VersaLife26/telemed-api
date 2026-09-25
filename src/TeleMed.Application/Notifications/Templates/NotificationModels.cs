namespace TeleMed.Application.Notifications.Templates;

public interface INotificationModel
{
    string TemplateKey { get; }
    IReadOnlyDictionary<string, string> Values();
}

public sealed record BookingConfirmedModel(string DoctorName, DateTimeOffset StartAt, long FeeCents, string Currency) : INotificationModel
{
    public string TemplateKey => TemplateKeys.BookingConfirmed;

    public IReadOnlyDictionary<string, string> Values() => new Dictionary<string, string>
    {
        ["DoctorName"] = DoctorName,
        ["DateTime"] = NotificationFormat.DateTime(StartAt),
        ["Fee"] = NotificationFormat.Money(FeeCents, Currency),
    };
}

public sealed record AppointmentCancelledModel(string DoctorName, DateTimeOffset StartAt) : INotificationModel
{
    public string TemplateKey => TemplateKeys.AppointmentCancelled;

    public IReadOnlyDictionary<string, string> Values() => new Dictionary<string, string>
    {
        ["DoctorName"] = DoctorName,
        ["DateTime"] = NotificationFormat.DateTime(StartAt),
    };
}

public sealed record AppointmentCancelledForDoctorModel(DateTimeOffset StartAt) : INotificationModel
{
    public string TemplateKey => TemplateKeys.AppointmentCancelledForDoctor;

    public IReadOnlyDictionary<string, string> Values() => new Dictionary<string, string>
    {
        ["DateTime"] = NotificationFormat.DateTime(StartAt),
    };
}

public sealed record ReminderModel(string TemplateKey, string DoctorName, DateTimeOffset StartAt) : INotificationModel
{
    public static ReminderModel DayBefore(string doctorName, DateTimeOffset startAt) => new(TemplateKeys.Reminder24h, doctorName, startAt);

    public static ReminderModel HourBefore(string doctorName, DateTimeOffset startAt) => new(TemplateKeys.Reminder1h, doctorName, startAt);

    public IReadOnlyDictionary<string, string> Values() => new Dictionary<string, string>
    {
        ["DoctorName"] = DoctorName,
        ["DateTime"] = NotificationFormat.DateTime(StartAt),
    };
}

public sealed record PaymentFailedModel(long AmountCents, string Currency) : INotificationModel
{
    public string TemplateKey => TemplateKeys.PaymentFailed;

    public IReadOnlyDictionary<string, string> Values() => new Dictionary<string, string>
    {
        ["Amount"] = NotificationFormat.Money(AmountCents, Currency),
    };
}

public sealed record PrescriptionReadyModel(string DoctorName, string DownloadUrl) : INotificationModel
{
    public string TemplateKey => TemplateKeys.PrescriptionReady;

    public IReadOnlyDictionary<string, string> Values() => new Dictionary<string, string>
    {
        ["DoctorName"] = DoctorName,
        ["DownloadUrl"] = DownloadUrl,
    };
}

public sealed record DoctorApplicationModel(string TemplateKey, string DoctorName, string? Reason = null) : INotificationModel
{
    public static DoctorApplicationModel Submitted(string doctorName) => new(TemplateKeys.DoctorApplicationSubmitted, doctorName);

    public static DoctorApplicationModel Approved(string doctorName) => new(TemplateKeys.DoctorApplicationApproved, doctorName);

    public static DoctorApplicationModel Rejected(string doctorName, string reason) => new(TemplateKeys.DoctorApplicationRejected, doctorName, reason);

    public IReadOnlyDictionary<string, string> Values() => new Dictionary<string, string>
    {
        ["DoctorName"] = DoctorName,
        ["Reason"] = Reason ?? "",
    };
}

public sealed record RescheduleRequestedModel(string DoctorName, DateTimeOffset OriginalStartAt, DateTimeOffset ProposedStartAt) : INotificationModel
{
    public string TemplateKey => TemplateKeys.RescheduleRequested;

    public IReadOnlyDictionary<string, string> Values() => new Dictionary<string, string>
    {
        ["DoctorName"] = DoctorName,
        ["DateTime"] = NotificationFormat.DateTime(OriginalStartAt),
        ["ProposedDateTime"] = NotificationFormat.DateTime(ProposedStartAt),
    };
}

public sealed record RescheduleConfirmedModel(string DoctorName, DateTimeOffset StartAt) : INotificationModel
{
    public string TemplateKey => TemplateKeys.RescheduleConfirmed;

    public IReadOnlyDictionary<string, string> Values() => new Dictionary<string, string>
    {
        ["DoctorName"] = DoctorName,
        ["DateTime"] = NotificationFormat.DateTime(StartAt),
    };
}

public sealed record RescheduleAcceptedForDoctorModel(DateTimeOffset StartAt) : INotificationModel
{
    public string TemplateKey => TemplateKeys.RescheduleAcceptedForDoctor;

    public IReadOnlyDictionary<string, string> Values() => new Dictionary<string, string>
    {
        ["DateTime"] = NotificationFormat.DateTime(StartAt),
    };
}

public sealed record DoctorRunningLateModel(string DoctorName) : INotificationModel
{
    public string TemplateKey => TemplateKeys.DoctorRunningLate;

    public IReadOnlyDictionary<string, string> Values() => new Dictionary<string, string> { ["DoctorName"] = DoctorName };
}

public sealed record EarlyJoinOfferedModel(string DoctorName, string JoinLink) : INotificationModel
{
    public string TemplateKey => TemplateKeys.EarlyJoinOffered;

    public IReadOnlyDictionary<string, string> Values() => new Dictionary<string, string>
    {
        ["DoctorName"] = DoctorName,
        ["JoinLink"] = JoinLink,
    };
}

public sealed record PaymentRefundedModel(long AmountCents, string Currency) : INotificationModel
{
    public string TemplateKey => TemplateKeys.PaymentRefunded;

    public IReadOnlyDictionary<string, string> Values() => new Dictionary<string, string>
    {
        ["Amount"] = NotificationFormat.Money(AmountCents, Currency),
    };
}

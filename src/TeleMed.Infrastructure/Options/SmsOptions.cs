namespace TeleMed.Infrastructure.Options;

public enum SmsProvider
{
    None,
    Dialog,
    Capture,
}

public sealed class SmsOptions
{
    public const string Section = "Sms";

    public SmsProvider Provider { get; set; } = SmsProvider.None;
    public DialogSmsOptions Dialog { get; set; } = new();
}

public sealed class DialogSmsOptions
{
    public string BaseUrl { get; set; } = "";
    public string ApplicationId { get; set; } = "";
    public string Password { get; set; } = "";
    public string? SourceAddress { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}

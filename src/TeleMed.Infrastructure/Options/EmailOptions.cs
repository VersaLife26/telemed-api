using MailKit.Security;

namespace TeleMed.Infrastructure.Options;

public enum EmailProvider
{
    None,
    Smtp,
    Capture,
}

public sealed class EmailOptions
{
    public const string Section = "Email";

    public EmailProvider Provider { get; set; } = EmailProvider.None;
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "VersaLife";
    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

namespace TeleMed.Infrastructure.Options;

public sealed class OtpOptions
{
    public const string Section = "Otp";

    public string HmacKey { get; set; } = "";
    public string? FixedCode { get; set; }
}

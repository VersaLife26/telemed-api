namespace TeleMed.Infrastructure.Options;

public sealed class PaymentsOptions
{
    public const string Section = "Payments";

    public PayHereOptions PayHere { get; set; } = new();
    public MockPaymentOptions Mock { get; set; } = new();
}

public sealed class PayHereOptions
{
    public bool Enabled { get; set; }
    public string MerchantId { get; set; } = "";
    public string MerchantSecret { get; set; } = "";
    // The merchant REST API (capture, refund) uses its own OAuth client credentials.
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string BaseUrl { get; set; } = "https://sandbox.payhere.lk";
    public string NotifyUrl { get; set; } = "";
    public string ReturnUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

    public bool IsValid() =>
        !Enabled || (
            MerchantId.Length > 0 && MerchantSecret.Length > 0 && AppId.Length > 0 && AppSecret.Length > 0
            && new[] { BaseUrl, NotifyUrl, ReturnUrl, CancelUrl }.All(IsHttpUrl)
            && Timeout > TimeSpan.Zero);

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}

public sealed class MockPaymentOptions
{
    public bool Enabled { get; set; }
    public bool AutoSucceed { get; set; }
}

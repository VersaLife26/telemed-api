namespace TeleMed.Infrastructure.Options;

public sealed class CryptoOptions
{
    public const string Section = "Crypto";
    public const int KeyBytes = 32;

    public string BankDataKey { get; set; } = "";

    public byte[]? BankDataKeyBytes()
    {
        var buffer = new byte[KeyBytes + 1];
        return Convert.TryFromBase64String(BankDataKey, buffer, out var written) && written == KeyBytes ? buffer[..KeyBytes] : null;
    }
}

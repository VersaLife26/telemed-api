namespace TeleMed.Infrastructure.Options;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    public string RootPath { get; set; } = "";
    public string SigningKey { get; set; } = "";
    public TimeSpan UrlTtl { get; set; } = TimeSpan.FromMinutes(5);
}

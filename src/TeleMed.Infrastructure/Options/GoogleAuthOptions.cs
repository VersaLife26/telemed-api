namespace TeleMed.Infrastructure.Options;

public sealed class GoogleAuthOptions
{
    public const string Section = "Auth:Google";

    public string[] ClientIds { get; set; } = [];
}

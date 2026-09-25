namespace TeleMed.Infrastructure.Options;

public sealed class JobOptions
{
    public bool Enabled { get; set; } = true;
    public TimeSpan Interval { get; set; }
}

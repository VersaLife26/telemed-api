namespace TeleMed.Infrastructure.Options;

public sealed class TestingOptions
{
    public const string Section = "Testing";
    public const int MinSecretLength = 16;

    public string SharedSecret { get; set; } = "";
    public TestFeatureOptions CaptureInbox { get; set; } = new();
    public TestFeatureOptions InstantMeetings { get; set; } = new();

    public bool AnyEnabled => CaptureInbox.Enabled || InstantMeetings.Enabled;
}

public sealed class TestFeatureOptions
{
    public bool Enabled { get; set; }
}

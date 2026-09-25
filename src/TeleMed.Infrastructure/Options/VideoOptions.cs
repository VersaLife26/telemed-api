using System.Text;

namespace TeleMed.Infrastructure.Options;

public sealed class VideoOptions
{
    public const string Section = "Video";
    public const int MinKeyBytes = 32;

    public string RoomTokenKey { get; set; } = "";
    public TimeSpan RoomTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public byte[] KeyBytes => Encoding.UTF8.GetBytes(RoomTokenKey);
}

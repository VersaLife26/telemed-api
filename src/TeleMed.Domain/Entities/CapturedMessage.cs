using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class CapturedMessage : Entity
{
    public MessageChannel Channel { get; init; }
    public required string Recipient { get; init; }
    public string? Subject { get; init; }
    public required string Body { get; init; }
}

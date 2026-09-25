using TeleMed.Domain.Common;

namespace TeleMed.Domain.Entities;

public class Specialty : ITimestamped, IAuditable
{
    public required string Code { get; init; }
    public required string NameEn { get; set; }
    public required string NameSi { get; set; }
    public required string NameTa { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

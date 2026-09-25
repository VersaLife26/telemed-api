using TeleMed.Domain.Common;

namespace TeleMed.Domain.Entities;

public class Icd10Code : ITimestamped
{
    public required string Code { get; init; }
    public required string Display { get; set; }
    public required string Category { get; set; }
    public string Synonyms { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

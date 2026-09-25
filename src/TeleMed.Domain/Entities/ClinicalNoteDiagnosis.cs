using TeleMed.Domain.Common;

namespace TeleMed.Domain.Entities;

// The display text is captured when the doctor picks the code, so a signed record still reads the same after a reseed.
public class ClinicalNoteDiagnosis : Entity
{
    public Guid NoteId { get; init; }
    public required string Code { get; init; }
    public required string Display { get; init; }
    public bool IsPrimary { get; init; }
    public int SortOrder { get; init; }
}

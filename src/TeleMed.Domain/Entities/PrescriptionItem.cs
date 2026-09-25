using TeleMed.Domain.Common;

namespace TeleMed.Domain.Entities;

public class PrescriptionItem : Entity
{
    public Guid PrescriptionId { get; init; }
    public Guid? DrugId { get; init; }
    public required string DrugName { get; init; }
    public string Strength { get; init; } = "";
    public string Form { get; init; } = "";
    public required string Dosage { get; init; }
    public required string Frequency { get; init; }
    public int DurationDays { get; init; }
    public int Quantity { get; init; }
    public string? Instructions { get; init; }
    public bool IsGeneric { get; init; }
    public int SortOrder { get; init; }
}

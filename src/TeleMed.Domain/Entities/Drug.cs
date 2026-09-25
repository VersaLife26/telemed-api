using TeleMed.Domain.Common;

namespace TeleMed.Domain.Entities;

public class Drug : Entity, IAuditable
{
    public required string Name { get; set; }
    public required string GenericName { get; set; }
    public required string Strength { get; set; }
    public required string Form { get; set; }
    public string? Manufacturer { get; set; }
    public string? Category { get; set; }
    public bool IsControlled { get; set; }
    public bool IsGeneric { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

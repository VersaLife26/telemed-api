namespace TeleMed.Application.Reference;

public sealed record SpecialtyDto(string Code, string NameEn, string NameSi, string NameTa, int DisplayOrder);

public sealed record DrugDto(
    Guid Id,
    string Name,
    string GenericName,
    string Strength,
    string Form,
    string? Manufacturer,
    string? Category,
    bool IsControlled,
    bool IsGeneric);

public sealed record Icd10CodeDto(string Code, string Display, string Category);

public sealed record SearchQuery
{
    public const int MaxLimit = 50;

    public string Q { get; init; } = "";
    public int Limit { get; init; } = 20;
}

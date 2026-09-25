using Riok.Mapperly.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Reference;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class ReferenceMapper
{
    public static partial SpecialtyDto ToDto(this Specialty specialty);
    public static partial DrugDto ToDto(this Drug drug);
    public static partial Icd10CodeDto ToDto(this Icd10Code code);
}

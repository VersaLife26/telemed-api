using Riok.Mapperly.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Admin.Content;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class ContentMapper
{
    public static partial AdminSpecialtyDto ToAdminDto(this Specialty specialty);
    public static partial AdminDrugDto ToAdminDto(this Drug drug);
}

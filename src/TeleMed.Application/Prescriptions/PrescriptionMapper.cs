using Riok.Mapperly.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Prescriptions;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class PrescriptionMapper
{
    public static PrescriptionDto ToDto(this Prescription prescription) =>
        prescription.Map() with { Items = prescription.Items.OrderBy(i => i.SortOrder).Select(i => i.ToDto()).ToList() };

    public static partial PrescriptionItemDto ToDto(this PrescriptionItem item);

    public static partial VerifiedItemDto ToVerifiedDto(this PrescriptionItem item);

    [MapperIgnoreTarget(nameof(PrescriptionDto.Items))]
    private static partial PrescriptionDto Map(this Prescription prescription);
}

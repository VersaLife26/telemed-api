using Riok.Mapperly.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Reschedules;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class RescheduleMapper
{
    public static partial RescheduleRequestDto ToDto(this RescheduleRequest request);
}

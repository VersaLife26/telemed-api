using Riok.Mapperly.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Consultations;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class ConsultationMapper
{
    public static partial ConsultationDto ToDto(this Consultation consultation);

    public static partial ConsultationMessageDto ToDto(this ConsultationMessage message);
}

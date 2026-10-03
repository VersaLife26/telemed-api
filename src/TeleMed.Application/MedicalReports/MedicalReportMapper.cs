using Riok.Mapperly.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.MedicalReports;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class MedicalReportMapper
{
    public static partial MedicalReportDto ToDto(this MedicalReport report);
}

using Riok.Mapperly.Abstractions;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Admin.Credentialing;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class CredentialingMapper
{
    public static DoctorApplicationDto ToDto(this DoctorApplication application, IEnumerable<DoctorDocument> documents) =>
        application.Map() with
        {
            HasPassword = application.PasswordHash is not null,
            Documents = documents.Select(d => d.ToDto()).ToList(),
        };

    public static partial DoctorApplicationSummaryDto ToSummaryDto(this DoctorApplication application);

    [MapperIgnoreTarget(nameof(DoctorApplicationDto.HasPassword))]
    [MapperIgnoreTarget(nameof(DoctorApplicationDto.Documents))]
    private static partial DoctorApplicationDto Map(this DoctorApplication application);
}

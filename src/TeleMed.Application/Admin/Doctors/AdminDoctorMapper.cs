using Riok.Mapperly.Abstractions;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Admin.Doctors;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class AdminDoctorMapper
{
    public static AdminDoctorDto ToAdminDto(this Doctor doctor, User? user, IEnumerable<DoctorDocument> documents, IFileStorage storage) =>
        doctor.Map() with
        {
            Phone = user?.PhoneNumber,
            Email = user?.Email,
            PhotoUrl = DoctorMapper.PhotoUrl(doctor, storage),
            Documents = documents.Select(d => d.ToDto()).ToList(),
        };

    [MapperIgnoreTarget(nameof(AdminDoctorDto.Phone))]
    [MapperIgnoreTarget(nameof(AdminDoctorDto.Email))]
    [MapperIgnoreTarget(nameof(AdminDoctorDto.PhotoUrl))]
    [MapperIgnoreTarget(nameof(AdminDoctorDto.Documents))]
    private static partial AdminDoctorDto Map(this Doctor doctor);
}

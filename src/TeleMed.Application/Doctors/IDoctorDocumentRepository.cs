using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Doctors;

public interface IDoctorDocumentRepository
{
    void Add(DoctorDocument document);
    Task<DoctorDocument?> FindLiveAsync(Guid id, CancellationToken ct);
    Task<DoctorDocument?> FindLiveForDoctorAsync(Guid doctorId, DoctorDocumentType type, CancellationToken ct);
    Task<DoctorDocument?> FindLiveForApplicationAsync(Guid applicationId, DoctorDocumentType type, CancellationToken ct);
    Task<IReadOnlyList<DoctorDocument>> ListLiveForDoctorAsync(Guid doctorId, CancellationToken ct);
    Task<IReadOnlyList<DoctorDocument>> ListLiveForApplicationAsync(Guid applicationId, CancellationToken ct);
}

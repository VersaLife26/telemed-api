using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.DoctorApplications;

public interface IDoctorApplicationRepository
{
    void Add(DoctorApplication application);
    Task<DoctorApplication?> FindAsync(Guid id, CancellationToken ct);
    Task<DoctorApplication?> FindLatestByPhoneAsync(string phone, CancellationToken ct);
    Task<(IReadOnlyList<DoctorApplication> Items, long Total)> ListAsync(DoctorApplicationStatus? status, int skip, int take, CancellationToken ct);
}

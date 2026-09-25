using TeleMed.Application.Admin.Doctors;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Doctors;

public interface IDoctorRepository
{
    void Add(Doctor doctor);
    Task<Doctor?> FindAsync(Guid id, CancellationToken ct);
    Task<Doctor?> FindByUserIdAsync(Guid userId, CancellationToken ct);
    Task<Doctor?> FindByUserPhoneAsync(string phone, CancellationToken ct);
    Task<bool> SlmcNumberExistsAsync(string slmcNumber, CancellationToken ct);

    Task<Doctor?> FindListedAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<Doctor> Items, long Total)> SearchListedAsync(DoctorSearchFilter filter, CancellationToken ct);

    Task<(IReadOnlyList<AdminDoctorListItemDto> Items, long Total)> ListForAdminAsync(DoctorStatus? status, string? search, int skip, int take, CancellationToken ct);
}

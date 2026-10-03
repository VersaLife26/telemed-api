using TeleMed.Domain.Entities;

namespace TeleMed.Application.MedicalReports;

public interface IMedicalReportRepository
{
    void Add(MedicalReport report);
    Task<MedicalReport?> FindAsync(Guid id, CancellationToken ct);
    Task<MedicalReport?> FindForUpdateAsync(Guid id, CancellationToken ct);
    Task<MedicalReport?> FindByAppointmentAsync(Guid appointmentId, CancellationToken ct);
    Task<(IReadOnlyList<MedicalReport> Items, long Total)> ListAsync(Guid? patientId, Guid? doctorId, int skip, int take, CancellationToken ct);
}

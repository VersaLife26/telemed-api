using TeleMed.Domain.Entities;

namespace TeleMed.Application.Prescriptions;

public interface IPrescriptionRepository
{
    void Add(Prescription prescription);
    // All of these load the items in their saved order.
    Task<Prescription?> FindAsync(Guid id, CancellationToken ct);
    Task<Prescription?> FindForUpdateAsync(Guid id, CancellationToken ct);
    Task<Prescription?> FindByAppointmentAsync(Guid appointmentId, CancellationToken ct);
    // Newest first; filters by whichever of patient or doctor is given.
    Task<(IReadOnlyList<Prescription> Items, long Total)> ListAsync(Guid? patientId, Guid? doctorId, int skip, int take, CancellationToken ct);
}

using TeleMed.Domain.Entities;

namespace TeleMed.Application.Vault;

public interface IRecordAccessRepository
{
    void Add(RecordAccessLog log);
    // The doctor's started consultation with the patient that was seen last (ended, or started when it never ended).
    Task<TreatingConsultation?> FindLatestTreatingAsync(Guid doctorId, Guid patientId, CancellationToken ct);
    Task<IReadOnlyList<TreatedPatient>> ListTreatedPatientsAsync(Guid doctorId, DateTimeOffset lastSeenAfter, CancellationToken ct);
}

public sealed record TreatingConsultation(DateTimeOffset StartedAt, DateTimeOffset? EndedAt);

public sealed record TreatedPatient(Guid PatientId, string FullName, DateTimeOffset LastSeenAt);

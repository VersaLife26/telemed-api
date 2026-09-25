using TeleMed.Domain.Entities;

namespace TeleMed.Application.Reference;

public interface IReferenceDataRepository
{
    Task<IReadOnlyList<Specialty>> ListActiveSpecialtiesAsync(CancellationToken ct);
    Task<bool> IsActiveSpecialtyAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<Drug>> SearchDrugsAsync(string prefix, int limit, CancellationToken ct);
    Task<IReadOnlyList<Icd10Code>> SearchIcd10Async(string? codePrefix, string? tsQuery, int limit, CancellationToken ct);
    Task<IReadOnlyList<Icd10Code>> FindIcd10Async(IReadOnlyCollection<string> codes, CancellationToken ct);
    Task<IReadOnlyList<Drug>> FindDrugsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}

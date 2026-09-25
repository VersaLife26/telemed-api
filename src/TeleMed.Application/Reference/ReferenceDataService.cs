using System.Text.RegularExpressions;

namespace TeleMed.Application.Reference;

public sealed partial class ReferenceDataService(IReferenceDataRepository repository)
{
    public async Task<IReadOnlyList<SpecialtyDto>> ListSpecialtiesAsync(CancellationToken ct) =>
        (await repository.ListActiveSpecialtiesAsync(ct)).Select(s => s.ToDto()).ToList();

    public async Task<IReadOnlyList<DrugDto>> SearchDrugsAsync(SearchQuery query, CancellationToken ct)
    {
        var term = query.Q.Trim();
        if (term.Length < 2)
        {
            return [];
        }

        return (await repository.SearchDrugsAsync(term, query.Limit, ct)).Select(d => d.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<Icd10CodeDto>> SearchIcd10Async(SearchQuery query, CancellationToken ct)
    {
        var term = query.Q.Trim();
        if (term.Length < 2)
        {
            return [];
        }

        var codePrefix = Icd10CodePrefix().IsMatch(term) ? term.ToUpperInvariant() : null;
        var tokens = NonLexemeChars().Split(term.ToLowerInvariant()).Where(t => t.Length > 0).ToList();
        var tsQuery = tokens.Count == 0 ? null : string.Join(" & ", tokens.Select(t => t + ":*"));
        if (codePrefix is null && tsQuery is null)
        {
            return [];
        }

        return (await repository.SearchIcd10Async(codePrefix, tsQuery, query.Limit, ct)).Select(c => c.ToDto()).ToList();
    }

    [GeneratedRegex(@"^[A-Za-z]\d{1,2}\.?\d{0,2}$")]
    private static partial Regex Icd10CodePrefix();

    // Stripping to [a-z0-9] guarantees the string handed to to_tsquery cannot contain a tsquery operator.
    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonLexemeChars();
}

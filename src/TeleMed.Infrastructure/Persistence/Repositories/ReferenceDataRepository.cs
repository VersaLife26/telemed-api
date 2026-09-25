using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Reference;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class ReferenceDataRepository(AppDbContext db) : IReferenceDataRepository
{
    public async Task<IReadOnlyList<Specialty>> ListActiveSpecialtiesAsync(CancellationToken ct) =>
        await db.Specialties.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Code)
            .ToListAsync(ct);

    public Task<bool> IsActiveSpecialtyAsync(string code, CancellationToken ct) =>
        db.Specialties.AnyAsync(s => s.Code == code && s.IsActive, ct);

    public async Task<IReadOnlyList<Drug>> SearchDrugsAsync(string prefix, int limit, CancellationToken ct)
    {
        var lowered = prefix.ToLowerInvariant();
        return await db.Drugs.AsNoTracking()
            .Where(d => d.IsActive && (d.Name.ToLower().StartsWith(lowered) || d.GenericName.ToLower().StartsWith(lowered)))
            .OrderBy(d => d.Name).ThenBy(d => d.Strength)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Icd10Code>> SearchIcd10Async(string? codePrefix, string? tsQuery, int limit, CancellationToken ct)
    {
        var prefix = codePrefix ?? "";
        var query = tsQuery ?? "";
        return await db.Icd10Codes
            .FromSql($"""
                SELECT c.*
                FROM icd10_codes c,
                     (SELECT {prefix}::text AS prefix,
                             CASE WHEN {query}::text = '' THEN NULL ELSE to_tsquery('english', {query}::text) END AS tsq) a
                WHERE (a.prefix <> '' AND c.code LIKE a.prefix || '%')
                   OR (a.tsq IS NOT NULL AND c.search_vector @@ a.tsq)
                ORDER BY
                    (a.prefix <> '' AND c.code = a.prefix) DESC,
                    (a.prefix <> '' AND c.code LIKE a.prefix || '%') DESC,
                    CASE WHEN a.tsq IS NULL THEN 0 ELSE ts_rank(c.search_vector, a.tsq) END DESC,
                    length(c.code),
                    c.code
                LIMIT {limit}
                """)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Icd10Code>> FindIcd10Async(IReadOnlyCollection<string> codes, CancellationToken ct) =>
        await db.Icd10Codes.AsNoTracking().Where(c => codes.Contains(c.Code)).ToListAsync(ct);

    public async Task<IReadOnlyList<Drug>> FindDrugsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Drugs.AsNoTracking().Where(d => ids.Contains(d.Id)).ToListAsync(ct);
}

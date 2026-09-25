using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class Icd10CodeConfiguration : IEntityTypeConfiguration<Icd10Code>
{
    public const string SearchVector = "SearchVector";

    public void Configure(EntityTypeBuilder<Icd10Code> builder)
    {
        builder.ToTable("icd10_codes");
        builder.HasKey(c => c.Code);

        // Rubric words outrank code tokens, which outrank synonyms, so "diabetes" ranks the real diagnosis above mentions.
        builder.Property<NpgsqlTsVector>(SearchVector)
            .HasComputedColumnSql(
                "setweight(to_tsvector('english', display), 'A') || " +
                "setweight(to_tsvector('english', replace(code, '.', ' ')), 'B') || " +
                "setweight(to_tsvector('english', synonyms), 'C')",
                stored: true);
        builder.HasIndex(SearchVector).HasMethod("GIN");
    }
}

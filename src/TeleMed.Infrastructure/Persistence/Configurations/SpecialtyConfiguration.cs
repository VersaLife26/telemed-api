using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class SpecialtyConfiguration : IEntityTypeConfiguration<Specialty>
{
    public void Configure(EntityTypeBuilder<Specialty> builder)
    {
        builder.ToTable("specialties");
        builder.HasKey(s => s.Code);
        builder.Property(s => s.Code).HasMaxLength(50);
        builder.Property(s => s.NameEn).HasMaxLength(100);
        builder.Property(s => s.NameSi).HasMaxLength(100);
        builder.Property(s => s.NameTa).HasMaxLength(100);
    }
}

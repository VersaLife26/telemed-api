using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class CapturedMessageConfiguration : IEntityTypeConfiguration<CapturedMessage>
{
    public void Configure(EntityTypeBuilder<CapturedMessage> builder)
    {
        builder.ToTable("captured_messages");
        builder.HasIndex(m => new { m.Recipient, m.CreatedAt });
    }
}

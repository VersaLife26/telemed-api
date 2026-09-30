using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class WaitingRoomItemConfiguration : IEntityTypeConfiguration<WaitingRoomItem>
{
    public void Configure(EntityTypeBuilder<WaitingRoomItem> builder)
    {
        builder.ToTable("waiting_room_items");
        builder.Property(i => i.Title).HasMaxLength(200);
        builder.Property(i => i.Body).HasMaxLength(20_000);
        builder.Property(i => i.LinkUrl).HasMaxLength(2_000);
        builder.Property(i => i.VideoUrl).HasMaxLength(2_000);
        builder.Property(i => i.ImageStorageKey).HasMaxLength(500);
        builder.HasIndex(i => i.DisplayOrder);
    }
}

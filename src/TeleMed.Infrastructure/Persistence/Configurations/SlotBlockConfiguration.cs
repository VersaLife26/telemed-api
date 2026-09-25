using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class SlotBlockConfiguration : IEntityTypeConfiguration<SlotBlock>
{
    public void Configure(EntityTypeBuilder<SlotBlock> builder)
    {
        builder.ToTable("slot_blocks", t => t.HasCheckConstraint("ck_slot_blocks_range", "end_at > start_at"));

        builder.Property(b => b.Reason).HasMaxLength(500);

        builder.HasOne<Doctor>().WithMany().HasForeignKey(b => b.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(b => b.CreatedByAdminId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.DoctorId, b.StartAt });
    }
}

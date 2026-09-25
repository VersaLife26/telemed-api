using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class VaultFolderConfiguration : IEntityTypeConfiguration<VaultFolder>
{
    // Created in the migration: (owner_id, parent_id, lower(name)) NULLS NOT DISTINCT among live folders.
    public const string SiblingNameIndex = "ux_vault_folders_sibling_name_live";

    public void Configure(EntityTypeBuilder<VaultFolder> builder)
    {
        builder.ToTable("vault_folders", t =>
            t.HasCheckConstraint("ck_vault_folders_name", $"char_length(btrim(name)) BETWEEN 1 AND {VaultFolder.MaxNameLength}"));
        builder.Property(f => f.Name).HasMaxLength(VaultFolder.MaxNameLength);
        builder.Property(f => f.Version).IsRowVersion();

        builder.HasOne<User>().WithMany().HasForeignKey(f => f.OwnerId)
            .HasConstraintName("fk_vault_folders_users_owner_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.CreatedBy)
            .HasConstraintName("fk_vault_folders_users_created_by").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VaultFolder>().WithMany().HasForeignKey(f => f.ParentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.OwnerId).HasFilter("deleted_at IS NULL");
        builder.HasIndex(f => f.ParentId);
    }
}

internal sealed class VaultDocumentConfiguration : IEntityTypeConfiguration<VaultDocument>
{
    public void Configure(EntityTypeBuilder<VaultDocument> builder)
    {
        builder.ToTable("vault_documents", t =>
            t.HasCheckConstraint("ck_vault_documents_size_bytes", $"size_bytes BETWEEN 1 AND {PlatformPolicy.VaultMaxDocumentBytes}"));
        builder.Property(d => d.StorageKey).HasMaxLength(500);
        builder.Property(d => d.FileName).HasMaxLength(VaultDocument.MaxFileNameLength);
        builder.Property(d => d.ContentType).HasMaxLength(100);
        builder.Property(d => d.Sha256).HasMaxLength(64).IsFixedLength();
        builder.Property(d => d.Version).IsRowVersion();

        builder.HasOne<User>().WithMany().HasForeignKey(d => d.OwnerId)
            .HasConstraintName("fk_vault_documents_users_owner_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.UploadedBy)
            .HasConstraintName("fk_vault_documents_users_uploaded_by").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VaultFolder>().WithMany().HasForeignKey(d => d.FolderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.OwnerId, d.CreatedAt }).HasFilter("deleted_at IS NULL");
        builder.HasIndex(d => d.FolderId).HasFilter("deleted_at IS NULL");
    }
}

internal sealed class RecordAccessLogConfiguration : IEntityTypeConfiguration<RecordAccessLog>
{
    public void Configure(EntityTypeBuilder<RecordAccessLog> builder)
    {
        builder.ToTable("record_access_logs");
        builder.Property(l => l.Id).UseSerialColumn();
        builder.Property(l => l.Reason).HasMaxLength(100);
        builder.Property(l => l.UserAgent).HasMaxLength(512);

        builder.HasIndex(l => new { l.OwnerId, l.CreatedAt });
        builder.HasIndex(l => new { l.ResourceType, l.ResourceId });
        builder.HasIndex(l => new { l.ActorId, l.CreatedAt });
        builder.HasIndex(l => l.CreatedAt).HasMethod("brin");
    }
}

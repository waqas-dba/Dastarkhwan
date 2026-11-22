using CoreKit.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreKit.Identity.Configuration;

public sealed class IdentityOutboxMessageConfiguration : IEntityTypeConfiguration<IdentityOutboxMessage>
{
    public void Configure(EntityTypeBuilder<IdentityOutboxMessage> builder)
    {
        builder.ToTable("Identity_OutboxMessages");

        builder.HasKey(x => x.EventId);
        builder.Property(x => x.EventId).ValueGeneratedNever();

        builder.Property(x => x.EventType).IsRequired().HasMaxLength(128);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.LeaseOwner).HasMaxLength(128);
        builder.Property(x => x.LastError).HasMaxLength(512);

        builder.HasIndex(x => new { x.Status, x.NextAttemptAt })
            .HasDatabaseName("IX_Identity_OutboxMessages_Status_NextAttemptAt");

        builder.HasIndex(x => x.SubjectUserId)
            .HasDatabaseName("IX_Identity_OutboxMessages_SubjectUserId");

        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("IX_Identity_OutboxMessages_CreatedAt");
    }
}
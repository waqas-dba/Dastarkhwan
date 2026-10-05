using Iam.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iam.Data.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.RefreshTokenHash).IsRequired().HasMaxLength(128);
        builder.Property(x => x.PreviousRefreshTokenHash).HasMaxLength(128);
        builder.Property(x => x.DeviceName).HasMaxLength(200);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.RevokedReason).HasMaxLength(100);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ClientApplication>()
            .WithMany()
            .HasForeignKey(x => x.ClientApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Only live sessions are ever looked up by user, so index just those.
        builder.HasIndex(x => new { x.UserId, x.ClientApplicationId })
            .HasFilter("\"RevokedAtUtc\" IS NULL");

        builder.HasIndex(x => x.ExpiresAtUtc); // for cleaning up old sessions

        // Two refreshes racing with the same token: only one of them can save.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
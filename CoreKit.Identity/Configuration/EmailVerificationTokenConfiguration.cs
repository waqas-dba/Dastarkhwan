using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreKit.Identity.Configuration;

public sealed class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("Idenity_EmailVerificationTokens");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        builder.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(320);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_Identity_EmailVerificationTokens_TokenHash");

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ExpiresAt); // for deleting old tokens
    }
}
using CoreKit.IAM.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Configuration;


public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("IAM_Users");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever(); // ids are created in code

        builder.Property(x => x.Email).IsRequired().HasMaxLength(320);
        builder.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(320);
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(x => x.DisplayName).HasMaxLength(200);

        // The database itself refuses a second account with the same email,
        // even if two requests race past the application check.
        builder.HasIndex(x => x.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("UX_IAM_Users_NormalizedEmail");
    }
}
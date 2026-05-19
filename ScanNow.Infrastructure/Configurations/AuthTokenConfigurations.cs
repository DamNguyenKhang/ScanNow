using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScanNow.Domain.Entities;

namespace ScanNow.Infrastructure.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("RefreshTokens");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Token).HasMaxLength(512).IsRequired();
            builder.Property(x => x.ReplacedByToken).HasMaxLength(512);
            builder.Property(x => x.IpAddress).HasMaxLength(100);
            builder.Property(x => x.IsRevoked).HasDefaultValue(false);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.UserId);
            builder.HasIndex(x => x.Token).IsUnique();
            builder.HasIndex(x => x.ExpiresAt);

            builder.HasOne(x => x.User)
                   .WithMany(x => x.RefreshTokens)
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
    {
        public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
        {
            builder.ToTable("PasswordResetTokens");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Token).HasMaxLength(512).IsRequired();
            builder.Property(x => x.IsUsed).HasDefaultValue(false);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.Token).IsUnique();
            builder.HasIndex(x => x.UserId);

            builder.HasOne(x => x.User)
                   .WithMany(x => x.PasswordResetTokens)
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

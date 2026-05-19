using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;

namespace ScanNow.Infrastructure.Configurations
{
    public class RestaurantTableConfiguration : IEntityTypeConfiguration<RestaurantTable>
    {
        public void Configure(EntityTypeBuilder<RestaurantTable> builder)
        {
            builder.ToTable("Tables");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.TableNumber).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Capacity).HasDefaultValue(4);
            builder.Property(x => x.QrCodeToken).HasMaxLength(512).IsRequired();
            builder.Property(x => x.QrCodeUrl).HasMaxLength(1000);
            builder.Property(x => x.QrCodeImageUrl).HasMaxLength(1000);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(TableStatus.AVAILABLE);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.BranchId);
            builder.HasIndex(x => x.QrCodeToken).IsUnique();
            builder.HasIndex(x => new { x.BranchId, x.TableNumber }).IsUnique();
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => x.IsActive);

            builder.HasOne(x => x.Branch)
                   .WithMany(x => x.Tables)
                   .HasForeignKey(x => x.BranchId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class QrSessionConfiguration : IEntityTypeConfiguration<QrSession>
    {
        public void Configure(EntityTypeBuilder<QrSession> builder)
        {
            builder.ToTable("QrSessions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.SessionToken).HasMaxLength(512).IsRequired();
            builder.Property(x => x.CustomerIdentifier).HasMaxLength(200);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.SessionToken).IsUnique();
            builder.HasIndex(x => x.TableId);
            builder.HasIndex(x => x.BranchId);
            builder.HasIndex(x => x.ActiveOrderId);
            builder.HasIndex(x => x.IsActive);
            builder.HasIndex(x => x.ExpiresAt);

            builder.HasOne(x => x.Table)
                   .WithMany(x => x.QrSessions)
                   .HasForeignKey(x => x.TableId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Branch)
                   .WithMany(x => x.QrSessions)
                   .HasForeignKey(x => x.BranchId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ActiveOrder)
                   .WithMany(x => x.QrSessions)
                   .HasForeignKey(x => x.ActiveOrderId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

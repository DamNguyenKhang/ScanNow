using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;

namespace ScanNow.Infrastructure.Configurations
{
    public class BranchPaymentConfigConfiguration : IEntityTypeConfiguration<BranchPaymentConfig>
    {
        public void Configure(EntityTypeBuilder<BranchPaymentConfig> builder)
        {
            builder.ToTable("BranchPaymentConfigs");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.CashEnabled).HasDefaultValue(true);
            builder.Property(x => x.PayOsEnabled).HasDefaultValue(false);
            builder.Property(x => x.PayOsClientId).HasMaxLength(200);
            builder.Property(x => x.PayOsApiKey).HasMaxLength(500);
            builder.Property(x => x.PayOsChecksumKey).HasMaxLength(500);
            builder.Property(x => x.DefaultMethod).HasConversion<string>().HasMaxLength(30).HasDefaultValue(PaymentMethod.CASH);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.BranchId).IsUnique();

            builder.HasOne(x => x.Branch)
                   .WithOne(x => x.PaymentConfig)
                   .HasForeignKey<BranchPaymentConfig>(x => x.BranchId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class PaperVoucherConfiguration : IEntityTypeConfiguration<PaperVoucher>
    {
        public void Configure(EntityTypeBuilder<PaperVoucher> builder)
        {
            builder.ToTable("PaperVouchers");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.DiscountType).HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(x => x.DiscountValue).HasColumnType("decimal(10,2)").IsRequired();
            builder.Property(x => x.MinOrderAmount).HasColumnType("decimal(10,2)").HasDefaultValue(0m);
            builder.Property(x => x.MaxDiscountAmount).HasColumnType("decimal(10,2)");
            builder.Property(x => x.Quantity).HasDefaultValue(1);
            builder.Property(x => x.UsedCount).HasDefaultValue(0);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.BranchId, x.Code }).IsUnique();
            builder.HasIndex(x => x.BranchId);
            builder.HasIndex(x => x.IsActive);
            builder.HasIndex(x => x.ValidUntil);

            builder.HasOne(x => x.Branch)
                   .WithMany(x => x.PaperVouchers)
                   .HasForeignKey(x => x.BranchId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

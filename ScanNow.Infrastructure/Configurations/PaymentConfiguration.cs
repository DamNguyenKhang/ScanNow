using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;

namespace ScanNow.Infrastructure.Configurations
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Amount).HasColumnType("decimal(10,2)").IsRequired();
            builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(PaymentStatus.PENDING).IsRequired();
            builder.Property(x => x.AmountReceived).HasColumnType("decimal(10,2)");
            builder.Property(x => x.ChangeAmount).HasColumnType("decimal(10,2)");
            builder.Property(x => x.TransactionId).HasMaxLength(200);
            builder.Property(x => x.GatewayOrderId).HasMaxLength(200);
            builder.Property(x => x.GatewayResponseCode).HasMaxLength(100);
            builder.Property(x => x.PaymentUrl).HasMaxLength(1000);
            builder.Property(x => x.RefundTransactionId).HasMaxLength(200);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.OrderId);
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => x.Method);
            builder.HasIndex(x => x.TransactionId);
            builder.HasIndex(x => x.GatewayOrderId);
            builder.HasIndex(x => x.CreatedAt);

            builder.HasOne(x => x.Order)
                   .WithMany(x => x.Payments)
                   .HasForeignKey(x => x.OrderId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.RefundedBy)
                   .WithMany(x => x.RefundedPayments)
                   .HasForeignKey(x => x.RefundedById)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ProcessedBy)
                   .WithMany(x => x.ProcessedPayments)
                   .HasForeignKey(x => x.ProcessedById)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

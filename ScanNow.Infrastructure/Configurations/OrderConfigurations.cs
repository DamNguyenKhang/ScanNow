using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;

namespace ScanNow.Infrastructure.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("Orders");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.OrderNumber).HasMaxLength(50).IsRequired();
            builder.Property(x => x.CustomerName).HasMaxLength(150);
            builder.Property(x => x.CustomerPhone).HasMaxLength(50);
            builder.Property(x => x.SubTotal).HasColumnType("decimal(10,2)").HasDefaultValue(0m).IsRequired();
            builder.Property(x => x.VatPercent).HasColumnType("decimal(5,2)").HasDefaultValue(0m);
            builder.Property(x => x.VatAmount).HasColumnType("decimal(10,2)").HasDefaultValue(0m);
            builder.Property(x => x.ServiceChargePercent).HasColumnType("decimal(5,2)").HasDefaultValue(0m);
            builder.Property(x => x.ServiceChargeAmount).HasColumnType("decimal(10,2)").HasDefaultValue(0m);
            builder.Property(x => x.DiscountAmount).HasColumnType("decimal(10,2)").HasDefaultValue(0m);
            builder.Property(x => x.TotalAmount).HasColumnType("decimal(10,2)").HasDefaultValue(0m).IsRequired();
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).HasDefaultValue(OrderStatus.PendingConfirmation).IsRequired();
            builder.Property(x => x.OrderSource).HasConversion<string>().HasMaxLength(20).HasDefaultValue(OrderSource.QR);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.BranchId);
            builder.HasIndex(x => x.TableId);
            builder.HasIndex(x => x.OrderNumber).IsUnique();
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => x.OrderSource);
            builder.HasIndex(x => x.CreatedAt);
            builder.HasIndex(x => new { x.BranchId, x.Status });
            builder.HasIndex(x => new { x.BranchId, x.CreatedAt });

            builder.HasOne(x => x.Branch)
                   .WithMany(x => x.Orders)
                   .HasForeignKey(x => x.BranchId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Table)
                   .WithMany(x => x.Orders)
                   .HasForeignKey(x => x.TableId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ConfirmedBy)
                   .WithMany(x => x.ConfirmedOrders)
                   .HasForeignKey(x => x.ConfirmedById)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.CancelledBy)
                   .WithMany(x => x.CancelledOrders)
                   .HasForeignKey(x => x.CancelledById)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.ToTable("OrderItems");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.MenuItemName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.UnitPrice).HasColumnType("decimal(10,2)").IsRequired();
            builder.Property(x => x.Quantity).HasDefaultValue(1).IsRequired();
            builder.Property(x => x.SubTotal).HasColumnType("decimal(10,2)").IsRequired();
            builder.Property(x => x.Note).HasMaxLength(500);
            builder.Property(x => x.EstimatedCookingMinutes).HasDefaultValue(0);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(OrderItemStatus.Pending);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.OrderId);
            builder.HasIndex(x => x.MenuItemId);
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => new { x.OrderId, x.Status });

            builder.HasOne(x => x.Order)
                   .WithMany(x => x.Items)
                   .HasForeignKey(x => x.OrderId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.MenuItem)
                   .WithMany(x => x.OrderItems)
                   .HasForeignKey(x => x.MenuItemId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

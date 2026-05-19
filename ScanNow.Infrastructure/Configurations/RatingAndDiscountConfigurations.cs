using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScanNow.Domain.Entities;

namespace ScanNow.Infrastructure.Configurations
{
    public class ItemRatingConfiguration : IEntityTypeConfiguration<ItemRating>
    {
        public void Configure(EntityTypeBuilder<ItemRating> builder)
        {
            builder.ToTable("ItemRatings");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Rating).IsRequired();
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.MenuItemId);
            builder.HasIndex(x => x.BranchId);
            builder.HasIndex(x => x.OrderId);
            builder.HasIndex(x => x.Rating);

            builder.HasOne(x => x.Order)
                   .WithMany(x => x.Ratings)
                   .HasForeignKey(x => x.OrderId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.OrderItem)
                   .WithMany(x => x.Ratings)
                   .HasForeignKey(x => x.OrderItemId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.MenuItem)
                   .WithMany(x => x.Ratings)
                   .HasForeignKey(x => x.MenuItemId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Branch)
                   .WithMany(x => x.ItemRatings)
                   .HasForeignKey(x => x.BranchId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class DiscountCodeConfiguration : IEntityTypeConfiguration<DiscountCode>
    {
        public void Configure(EntityTypeBuilder<DiscountCode> builder)
        {
            builder.ToTable("DiscountCodes");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.DiscountType).HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(x => x.DiscountValue).HasColumnType("decimal(10,2)").IsRequired();
            builder.Property(x => x.MinOrderAmount).HasColumnType("decimal(10,2)").HasDefaultValue(0m);
            builder.Property(x => x.MaxDiscountAmount).HasColumnType("decimal(10,2)");
            builder.Property(x => x.UsedCount).HasDefaultValue(0);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.BranchId, x.Code }).IsUnique();
            builder.HasIndex(x => x.BranchId);
            builder.HasIndex(x => x.IsActive);
            builder.HasIndex(x => x.ValidUntil);

            builder.HasOne(x => x.Branch)
                   .WithMany(x => x.DiscountCodes)
                   .HasForeignKey(x => x.BranchId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

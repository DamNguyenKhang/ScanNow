using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScanNow.Domain.Entities;

namespace ScanNow.Infrastructure.Configurations
{
    public class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.ToTable("Categories");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.ImageUrl).HasMaxLength(1000);
            builder.Property(x => x.DisplayOrder).HasDefaultValue(0);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.BranchId);
            builder.HasIndex(x => new { x.BranchId, x.DisplayOrder });
            builder.HasIndex(x => x.IsActive);

            builder.HasOne(x => x.Branch)
                   .WithMany(x => x.Categories)
                   .HasForeignKey(x => x.BranchId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
    {
        public void Configure(EntityTypeBuilder<MenuItem> builder)
        {
            builder.ToTable("MenuItems");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.ImageUrl).HasMaxLength(1000);
            builder.Property(x => x.Price).HasColumnType("decimal(10,2)").IsRequired();
            builder.Property(x => x.CostPrice).HasColumnType("decimal(10,2)").HasDefaultValue(0m);
            builder.Property(x => x.PreparationTime).HasDefaultValue(0);
            builder.Property(x => x.DisplayOrder).HasDefaultValue(0);
            builder.Property(x => x.IsAvailable).HasDefaultValue(true);
            builder.Property(x => x.IsFeatured).HasDefaultValue(false);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.BranchId);
            builder.HasIndex(x => x.CategoryId);
            builder.HasIndex(x => x.IsAvailable);
            builder.HasIndex(x => x.IsFeatured);
            builder.HasIndex(x => x.IsActive);
            builder.HasIndex(x => new { x.BranchId, x.CategoryId, x.DisplayOrder });

            builder.HasOne(x => x.Branch)
                   .WithMany(x => x.MenuItems)
                   .HasForeignKey(x => x.BranchId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Category)
                   .WithMany(x => x.MenuItems)
                   .HasForeignKey(x => x.CategoryId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class MenuItemPriceHistoryConfiguration : IEntityTypeConfiguration<MenuItemPriceHistory>
    {
        public void Configure(EntityTypeBuilder<MenuItemPriceHistory> builder)
        {
            builder.ToTable("MenuItemPriceHistory");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.OldPrice).HasColumnType("decimal(10,2)").IsRequired();
            builder.Property(x => x.NewPrice).HasColumnType("decimal(10,2)").IsRequired();
            builder.Property(x => x.Note).HasMaxLength(500);
            builder.Property(x => x.ChangedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.MenuItemId);
            builder.HasIndex(x => x.ChangedAt);

            builder.HasOne(x => x.MenuItem)
                   .WithMany(x => x.PriceHistories)
                   .HasForeignKey(x => x.MenuItemId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ChangedBy)
                   .WithMany(x => x.PriceChanges)
                   .HasForeignKey(x => x.ChangedById)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

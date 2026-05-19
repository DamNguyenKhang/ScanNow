using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScanNow.Domain.Entities;

namespace ScanNow.Infrastructure.Configurations
{
    public class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
    {
        public void Configure(EntityTypeBuilder<Restaurant> builder)
        {
            builder.ToTable("Restaurants");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            builder.Property(x => x.LogoUrl).HasMaxLength(1000);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.OwnerId);
            builder.HasIndex(x => x.Slug).IsUnique();

            builder.HasOne(x => x.Owner)
                   .WithMany(x => x.OwnedRestaurants)
                   .HasForeignKey(x => x.OwnerId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class BranchConfiguration : IEntityTypeConfiguration<Branch>
    {
        public void Configure(EntityTypeBuilder<Branch> builder)
        {
            builder.ToTable("Branches");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Address).HasMaxLength(500);
            builder.Property(x => x.Phone).HasMaxLength(50);
            builder.Property(x => x.Email).HasMaxLength(256);
            builder.Property(x => x.OpenTime).HasColumnType("time without time zone");
            builder.Property(x => x.CloseTime).HasColumnType("time without time zone");
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.VatPercent).HasColumnType("decimal(5,2)").HasDefaultValue(0m);
            builder.Property(x => x.ServiceChargePercent).HasColumnType("decimal(5,2)").HasDefaultValue(0m);
            builder.Property(x => x.ServiceChargeFixed).HasColumnType("decimal(10,2)").HasDefaultValue(0m);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.RestaurantId);
            builder.HasIndex(x => x.ManagerId);
            builder.HasIndex(x => new { x.RestaurantId, x.Slug }).IsUnique();
            builder.HasIndex(x => x.IsActive);

            builder.HasOne(x => x.Restaurant)
                   .WithMany(x => x.Branches)
                   .HasForeignKey(x => x.RestaurantId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Manager)
                   .WithMany(x => x.ManagedBranches)
                   .HasForeignKey(x => x.ManagerId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class BranchStaffConfiguration : IEntityTypeConfiguration<BranchStaff>
    {
        public void Configure(EntityTypeBuilder<BranchStaff> builder)
        {
            builder.ToTable("BranchStaff");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.AssignedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.BranchId, x.UserId }).IsUnique();
            builder.HasIndex(x => x.BranchId);
            builder.HasIndex(x => x.UserId);

            builder.HasOne(x => x.Branch)
                   .WithMany(x => x.Staffs)
                   .HasForeignKey(x => x.BranchId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.User)
                   .WithMany(x => x.BranchStaffAssignments)
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.AssignedBy)
                   .WithMany(x => x.AssignedBranchStaffs)
                   .HasForeignKey(x => x.AssignedById)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

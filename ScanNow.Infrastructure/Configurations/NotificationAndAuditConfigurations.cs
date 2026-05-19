using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;

namespace ScanNow.Infrastructure.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.TargetType).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
            builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Body).IsRequired();
            builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20).HasDefaultValue(NotificationChannel.SIGNALR);
            builder.Property(x => x.IsRead).HasDefaultValue(false);
            builder.Property(x => x.IsSent).HasDefaultValue(false);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.BranchId);
            builder.HasIndex(x => x.OrderId);
            builder.HasIndex(x => x.TargetUserId);
            builder.HasIndex(x => x.Type);
            builder.HasIndex(x => x.IsRead);
            builder.HasIndex(x => x.CreatedAt);
            builder.HasIndex(x => new { x.TargetType, x.BranchId });

            builder.HasOne(x => x.Branch)
                   .WithMany(x => x.Notifications)
                   .HasForeignKey(x => x.BranchId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Order)
                   .WithMany(x => x.Notifications)
                   .HasForeignKey(x => x.OrderId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.TargetUser)
                   .WithMany(x => x.TargetNotifications)
                   .HasForeignKey(x => x.TargetUserId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("AuditLogs");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            builder.Property(x => x.IpAddress).HasMaxLength(100);
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            builder.HasIndex(x => x.UserId);
            builder.HasIndex(x => x.BranchId);
            builder.HasIndex(x => x.EntityType);
            builder.HasIndex(x => x.EntityId);
            builder.HasIndex(x => x.Action);
            builder.HasIndex(x => x.CreatedAt);
            builder.HasIndex(x => new { x.EntityType, x.EntityId });

            builder.HasOne(x => x.User)
                   .WithMany(x => x.AuditLogs)
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Branch)
                   .WithMany(x => x.AuditLogs)
                   .HasForeignKey(x => x.BranchId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using TransportLines.Models;

namespace TransportLines.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Area> Areas { get; set; }

    public virtual DbSet<Driver> Drivers { get; set; }

    public virtual DbSet<Line> Lines { get; set; }

    public virtual DbSet<Lineday> Linedays { get; set; }

    public virtual DbSet<Lineprice> Lineprices { get; set; }

    public virtual DbSet<Linestop> Linestops { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Rating> Ratings { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Stop> Stops { get; set; }

    public virtual DbSet<Subscription> Subscriptions { get; set; }

    public virtual DbSet<TestResult> TestResults { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Vehicle> Vehicles { get; set; }

    public virtual DbSet<Vehicletype> Vehicletypes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_unicode_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Area>(entity =>
        {
            entity.HasKey(e => e.AreaId).HasName("PRIMARY");

            entity.Property(e => e.IsActive).HasDefaultValueSql("'1'");
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(e => e.DriverId).HasName("PRIMARY");

            entity.Property(e => e.VerificationStatus).HasDefaultValueSql("'Pending'");

            entity.HasOne(d => d.User).WithOne(p => p.DriverUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_drivers_user");

            entity.HasOne(d => d.VerifiedByUser).WithMany(p => p.DriverVerifiedByUsers).HasConstraintName("fk_drivers_verifier");
        });

        modelBuilder.Entity<Line>(entity =>
        {
            entity.HasKey(e => e.LineId).HasName("PRIMARY");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LineStatus).HasDefaultValueSql("'Open'");
            entity.Property(e => e.MinSubscriptionMonths).HasDefaultValueSql("'1'");

            entity.HasOne(d => d.Driver).WithMany(p => p.Lines)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_lines_driver");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.Lines)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_lines_vehicle");
        });

        modelBuilder.Entity<Lineday>(entity =>
        {
            entity.HasKey(e => e.LineDayId).HasName("PRIMARY");

            entity.HasOne(d => d.Line).WithMany(p => p.Linedays)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_linedays_line");
        });

        modelBuilder.Entity<Lineprice>(entity =>
        {
            entity.HasKey(e => e.LinePriceId).HasName("PRIMARY");

            entity.HasOne(d => d.Line).WithMany(p => p.Lineprices)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_lineprices_line");

            entity.HasOne(d => d.Linestop).WithMany(p => p.LinepriceLinestops)
                .HasPrincipalKey(p => new { p.LineId, p.StopId })
                .HasForeignKey(d => new { d.LineId, d.FromStopId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_lineprices_from");

            entity.HasOne(d => d.LinestopNavigation).WithMany(p => p.LinepriceLinestopNavigations)
                .HasPrincipalKey(p => new { p.LineId, p.StopId })
                .HasForeignKey(d => new { d.LineId, d.ToStopId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_lineprices_to");
        });

        modelBuilder.Entity<Linestop>(entity =>
        {
            entity.HasKey(e => e.LineStopId).HasName("PRIMARY");

            entity.HasOne(d => d.Line).WithMany(p => p.Linestops)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_linestops_line");

            entity.HasOne(d => d.Stop).WithMany(p => p.Linestops)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_linestops_stop");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PRIMARY");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.RelatedLine).WithMany(p => p.Notifications).HasConstraintName("fk_notifications_line");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_notifications_user");
        });

        modelBuilder.Entity<Rating>(entity =>
        {
            entity.HasKey(e => e.RatingId).HasName("PRIMARY");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Subscription).WithOne(p => p.Rating)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_ratings_subscription");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PRIMARY");
        });

        modelBuilder.Entity<Stop>(entity =>
        {
            entity.HasKey(e => e.StopId).HasName("PRIMARY");

            entity.Property(e => e.IsActive).HasDefaultValueSql("'1'");

            entity.HasOne(d => d.Area).WithMany(p => p.Stops)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_stops_area");
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.SubscriptionId).HasName("PRIMARY");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.OpenKey).HasComputedColumnSql("case when (`SubscriptionStatus` in (_utf8mb4'Pending',_utf8mb4'Active')) then 1 else NULL end", true);
            entity.Property(e => e.SubscriptionStatus).HasDefaultValueSql("'Pending'");

            entity.HasOne(d => d.Line).WithMany(p => p.Subscriptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_subscriptions_line");

            entity.HasOne(d => d.Passenger).WithMany(p => p.Subscriptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_subscriptions_passenger");

            entity.HasOne(d => d.Linestop).WithMany(p => p.SubscriptionLinestops)
                .HasPrincipalKey(p => new { p.LineId, p.StopId })
                .HasForeignKey(d => new { d.LineId, d.DropoffStopId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_subscriptions_dropoff");

            entity.HasOne(d => d.LinestopNavigation).WithMany(p => p.SubscriptionLinestopNavigations)
                .HasPrincipalKey(p => new { p.LineId, p.StopId })
                .HasForeignKey(d => new { d.LineId, d.PickupStopId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_subscriptions_pickup");
        });

        modelBuilder.Entity<TestResult>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity.Property(e => e.AccountStatus).HasDefaultValueSql("'Active'");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_users_role");
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.VehicleId).HasName("PRIMARY");

            entity.Property(e => e.IsActive).HasDefaultValueSql("'1'");

            entity.HasOne(d => d.Driver).WithMany(p => p.Vehicles)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_vehicles_driver");

            entity.HasOne(d => d.VehicleType).WithMany(p => p.Vehicles)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_vehicles_type");
        });

        modelBuilder.Entity<Vehicletype>(entity =>
        {
            entity.HasKey(e => e.VehicleTypeId).HasName("PRIMARY");

            entity.Property(e => e.IsActive).HasDefaultValueSql("'1'");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

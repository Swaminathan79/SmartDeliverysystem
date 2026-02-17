using Microsoft.EntityFrameworkCore;
using PackageService.Models;

namespace PackageService.Data;

public class PackageDbContext : DbContext
{
    public PackageDbContext(DbContextOptions<PackageDbContext> options) : base(options)
    {
    }

    public DbSet<Package> Packages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Package>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.TrackingNumber).IsUnique();
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.RouteId);

            entity.Property(e => e.TrackingNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.WeightKg).HasPrecision(10, 2);
            entity.Property(e => e.Status).HasConversion<string>();
        });

        // Seed data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Package>().HasData(
            new Package
            {
                Id = 1,
                TrackingNumber = "PKG-2026-0001",
                CustomerId = 100,
                RouteId = 1,
                Status = PackageStatus.Pending,
                WeightKg = 3.2m,
                CreatedAt = DateTime.UtcNow,
                Description = "Electronics"
            }
        );
    }
}
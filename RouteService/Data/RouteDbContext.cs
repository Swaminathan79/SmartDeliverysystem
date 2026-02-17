using Microsoft.EntityFrameworkCore;

using Route = Microsoft.AspNetCore.Routing.Route; //Microsoft.AspNetCore.Routing.Route;Microsoft.AspNetCore.Routing.Route;

namespace RouteService.Data;

public class RouteDbContext : DbContext
{
    public RouteDbContext(DbContextOptions<RouteDbContext> options) : base(options)
    {
    }
    
    public DbSet<RouteService.Models.Route> Routes { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<RouteService.Models.Route>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.StartLocation).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EndLocation).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EstimatedDistanceKm).HasPrecision(10, 2);
            
            // Index for common queries
            entity.HasIndex(e => new { e.DriverId, e.ScheduledDate });
        });
        
        // Seed data
        SeedData(modelBuilder);
    }
    
    private void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RouteService.Models.Route>().HasData(
            new RouteService.Models.Route
            {
                Id = 1,
                DriverId = 101,
                VehicleId = 1,
                StartLocation = "Warehouse A",
                EndLocation = "Downtown District",
                EstimatedDistanceKm = 15.5m,
                ScheduledDate = DateTime.UtcNow.AddDays(1)
            },
            new RouteService.Models.Route
            {
                Id = 2,
                DriverId = 102,
                VehicleId = 2,
                StartLocation = "Warehouse B",
                EndLocation = "Industrial Zone",
                EstimatedDistanceKm = 22.3m,
                ScheduledDate = DateTime.UtcNow.AddDays(2)
            }
        );
    }
}

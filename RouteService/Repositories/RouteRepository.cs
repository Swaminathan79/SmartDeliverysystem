using Microsoft.EntityFrameworkCore;
using RouteService.Data;
using Route = Microsoft.AspNetCore.Routing.Route; //RouteService.Models.Route; // RouteService.Models; // Microsoft.AspNetCore.Routing.Route;

namespace RouteService.Repositories;

public interface IRouteRepository
{
    Task<RouteService.Models.Route?> GetByIdAsync(int id);
    Task<IEnumerable<RouteService.Models.Route>> GetAllAsync();
    Task<IEnumerable<RouteService.Models.Route>> GetByDriverAsync(int driverId);
    Task<RouteService.Models.Route> AddAsync(RouteService.Models.Route route);
    Task UpdateAsync(RouteService.Models.Route route);
    Task DeleteAsync(int id);
    Task<bool> HasOverlappingRoutesAsync(int driverId, DateTime date, int? excludeRouteId = null);
    Task<(IEnumerable<RouteService.Models.Route> Routes, int TotalCount)> SearchAsync(
        DateTime? date,
        string? driverName,
        int? driverId,
        int pageNumber,
        int pageSize);
}

public class RouteRepository : IRouteRepository
{
    private readonly RouteDbContext _context;
    
    public RouteRepository(RouteDbContext context)
    {
        _context = context;
    }
    
    public async Task<RouteService.Models.Route?> GetByIdAsync(int id)
    {
        return await _context.Routes.FindAsync(id);
    }
    
    public async Task<IEnumerable<RouteService.Models.Route>> GetAllAsync()
    {
        return await _context.Routes.ToListAsync();
    }
    
    public async Task<IEnumerable<RouteService.Models.Route>> GetByDriverAsync(int driverId)
    {
        return await _context.Routes
            .Where(r => r.DriverId == driverId)
            .OrderBy(r => r.ScheduledDate)
            .ToListAsync();
    }
    
    public async Task<RouteService.Models.Route> AddAsync(RouteService.Models.Route route)
    {
        _context.Routes.Add(route);
        await _context.SaveChangesAsync();
        return route;
    }
    
    public async Task UpdateAsync(RouteService.Models.Route route)
    {
        _context.Routes.Update(route);
        await _context.SaveChangesAsync();
    }
    
    public async Task DeleteAsync(int id)
    {
        var route = await _context.Routes.FindAsync(id);
        if (route != null)
        {
            _context.Routes.Remove(route);
            await _context.SaveChangesAsync();
        }
    }
    
    public async Task<bool> HasOverlappingRoutesAsync(int driverId, DateTime date, int? excludeRouteId = null)
    {
        return await _context.Routes
            .AnyAsync(r =>
                r.DriverId == driverId &&
                r.ScheduledDate.Date == date.Date &&
                (!excludeRouteId.HasValue || r.Id != excludeRouteId.Value)
            );
    }
    
    public async Task<(IEnumerable<RouteService.Models.Route> Routes, int TotalCount)> SearchAsync(
        DateTime? date,
        string? driverName,
        int? driverId,
        int pageNumber,
        int pageSize)
    {
        var query = _context.Routes.AsQueryable();
        
        if (date.HasValue)
        {
            query = query.Where(r => r.ScheduledDate.Date == date.Value.Date);
        }
        
        if (driverId.HasValue)
        {
            query = query.Where(r => r.DriverId == driverId.Value);
        }
        
        // Note: DriverName filtering would require integration with AuthService
        // For now, we'll filter by driverId only
        
        var totalCount = await query.CountAsync();
        
        var routes = await query
            .OrderBy(r => r.ScheduledDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (routes, totalCount);
    }
}

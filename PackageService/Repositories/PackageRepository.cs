using Microsoft.EntityFrameworkCore;
using PackageService.Data;
using PackageService.Models;

namespace PackageService.Repositories;

public interface IPackageRepository
{
    Task<Package?> GetByIdAsync(int id);
    Task<Package?> GetByTrackingNumberAsync(string trackingNumber);
    Task<IEnumerable<Package>> GetAllAsync();
    Task<IEnumerable<Package>> GetByCustomerAsync(int customerId);
    Task<Package> AddAsync(Package package);
    Task UpdateAsync(Package package);
    Task DeleteAsync(int id);
    Task<(List<Package> Packages, int TotalCount)> SearchAsync(
        string? trackingNumber,
        PackageStatus? status,
        int? customerId,
        int pageNumber,
        int pageSize);
}

public class PackageRepository : IPackageRepository
{
    private readonly PackageDbContext _context;

    public PackageRepository(PackageDbContext context)
    {
        _context = context;
    }

    public async Task<Package?> GetByIdAsync(int id)
    {
        return await _context.Packages.FindAsync(id);
    }

    public async Task<Package?> GetByTrackingNumberAsync(string trackingNumber)
    {
        return await _context.Packages
            .FirstOrDefaultAsync(p => p.TrackingNumber == trackingNumber);
    }

    public async Task<IEnumerable<Package>> GetAllAsync()
    {
        return await _context.Packages.ToListAsync();
    }

    public async Task<IEnumerable<Package>> GetByCustomerAsync(int customerId)
    {
        return await _context.Packages
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Package> AddAsync(Package package)
    {
        _context.Packages.Add(package);
        await _context.SaveChangesAsync();
        return package;
    }

    public async Task UpdateAsync(Package package)
    {
        _context.Packages.Update(package);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var package = await _context.Packages.FindAsync(id);
        if (package != null)
        {
            _context.Packages.Remove(package);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<(List<Package> Packages, int TotalCount)> SearchAsync(
        string? trackingNumber,
        PackageStatus? status,
        int? customerId,
        int pageNumber,
        int pageSize)
    {
        var query = _context.Packages.AsQueryable();

        if (!string.IsNullOrEmpty(trackingNumber))
        {
            query = query.Where(p => p.TrackingNumber.Contains(trackingNumber));
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(p => p.CustomerId == customerId.Value);
        }

        var totalCount = await query.CountAsync();

        var packages = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (packages, totalCount);
    }
}
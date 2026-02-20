using PackageService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;


namespace PackageService.BuildingBlocks.Infrastructure
{
    public static class DatabaseInitializer
    {
      
        public static async Task ResetDatabaseAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<PackageDbContext>();

            // Delete entire InMemory database
            await context.Database.EnsureDeletedAsync();// deletes all data
            // Recreate database
            await context.Database.EnsureCreatedAsync();// recreate schema

            Log.Information("PackageService InMemory database reset successfully");
        }


        public static async Task ClearPackageServiceAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<PackageDbContext>();

            var users = await context.Packages.ToListAsync();

            var count = await context.Packages.CountAsync();
            Console.WriteLine($"🔥 Current package count: {count}");

            if(count > 0) { 
                context.Packages.RemoveRange(users);
                await context.SaveChangesAsync();
            }

        }


    }

}

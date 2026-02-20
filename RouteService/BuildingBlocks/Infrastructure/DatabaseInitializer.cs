using RouteService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;


namespace RouteService.BuildingBlocks.Infrastructure
{
    public static class DatabaseInitializer
    {
      
        public static async Task ResetDatabaseAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<RouteDbContext>();

            // Delete entire InMemory database
            await context.Database.EnsureDeletedAsync();// deletes all data
            // Recreate database
            await context.Database.EnsureCreatedAsync();// recreate schema

            Log.Information("RouteService InMemory database reset successfully");
        }


        public static async Task ClearRouteAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<RouteDbContext>();

            var users = await context.Routes.ToListAsync();

            var count = await context.Routes.CountAsync();
            Console.WriteLine($"🔥 Current route count: {count}");

            if(count > 0) { 
                context.Routes.RemoveRange(users);
                await context.SaveChangesAsync();
            }

        }


    }

}

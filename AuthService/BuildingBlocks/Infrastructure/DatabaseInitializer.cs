using AuthService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;


namespace AuthService.BuildingBlocks.Infrastructure
{
    public static class DatabaseInitializer
    {
      
        public static async Task ResetDatabaseAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

            // Delete entire InMemory database
            await context.Database.EnsureDeletedAsync();// deletes all data
            // Recreate database
            await context.Database.EnsureCreatedAsync();// recreate schema

            Log.Information("AuthService InMemory database reset successfully");
        }


        public static async Task ClearUsersAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

            var users = await context.Users.ToListAsync();

            var count = await context.Users.CountAsync();
            Console.WriteLine($"🔥 Current user count: {count}");

            if(count > 0) { 
                context.Users.RemoveRange(users);
                await context.SaveChangesAsync();
            }

        }


    }

}

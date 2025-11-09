using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Data;
using Data.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure
{
    public static class DatabaseSeederExtensions
    {
        /// <summary>
        /// Applies pending EF Core migrations (if any) and executes all registered ISeeder implementations
        /// within a scoped service provider. Intended to be called once during startup after builder.Build().
        /// </summary>
        public static async Task SeedDatabaseAsync(this IHost appHost)
        {
            // Create a scope so we resolve scoped services (DbContext, UserManager, RoleManager, etc.)
            using var scope = appHost.Services.CreateScope();
            var services = scope.ServiceProvider;

            // Use ILoggerFactory to create a category-based logger (avoids using a static class as generic arg)
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("DatabaseSeederExtensions");

            try
            {
                // Ensure the database schema is present before running seeders.
                var db = services.GetService<LogiTrackContext>();
                if (db != null)
                {
                    logger.LogInformation("Applying migrations (if any) before seeding...");
                    await db.Database.MigrateAsync();
                }

                // Resolve and run all ISeeder implementations from the scoped provider.
                var seeders = services.GetServices<ISeeder>();

                foreach (var seeder in seeders)
                {
                    logger.LogInformation("Running seeder {Seeder}", seeder.GetType().FullName);
                    await seeder.SeedAsync();
                }

                logger.LogInformation("Database seeding completed.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }
    }
}

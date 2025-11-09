using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Models;

namespace Data.Seeding
{
    public class RoleAndUserSeeder : ISeeder
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _env;
        private readonly ILogger<RoleAndUserSeeder> _logger;

        public RoleAndUserSeeder(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            IHostEnvironment env,
            ILogger<RoleAndUserSeeder> logger)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _configuration = configuration;
            _env = env;
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            // Ensure the core roles exist
            var rolesToEnsure = new[] { "User", "Manager" };

            foreach (var roleName in rolesToEnsure)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    var roleResult = await _roleManager.CreateAsync(new IdentityRole(roleName));
                    if (!roleResult.Succeeded)
                    {
                        _logger.LogError("Failed to create role {Role}: {Errors}", roleName, string.Join(';', roleResult.Errors));
                        // continue trying to create other roles; do not return immediately so we attempt all
                    }
                    else
                    {
                        _logger.LogInformation("Created role {Role}", roleName);
                    }
                }
            }

            // Read seed credentials (config first, then env)
            var seedEmail = _configuration["Seed:Manager:Email"] ?? Environment.GetEnvironmentVariable("SEED_MANAGER_EMAIL");
            var seedPassword = _configuration["Seed:Manager:Password"] ?? Environment.GetEnvironmentVariable("SEED_MANAGER_PASSWORD");
            var seedFirst = _configuration["Seed:Manager:FirstName"] ?? Environment.GetEnvironmentVariable("SEED_MANAGER_FIRSTNAME");
            var seedLast = _configuration["Seed:Manager:LastName"] ?? Environment.GetEnvironmentVariable("SEED_MANAGER_LASTNAME");
            var allowInProd = _configuration["Seed:AllowInProduction"] ?? Environment.GetEnvironmentVariable("SEED_ALLOW_IN_PRODUCTION");

            // If credentials not provided, skip creating the seeded manager user
            if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPassword))
            {
                _logger.LogInformation("Seed credentials not provided; skipping default manager creation.");
                return;
            }

            // Only allow creating the seeded manager in Development or when explicitly permitted
            var allowed = _env.IsDevelopment()
                          || (!string.IsNullOrWhiteSpace(allowInProd) && bool.TryParse(allowInProd, out var allow) && allow);

            if (!allowed)
            {
                _logger.LogWarning("Seeding of default manager skipped: environment not allowed.");
                return;
            }

            // Create or ensure manager user exists and is in the Manager role
            var user = await _userManager.FindByEmailAsync(seedEmail);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = seedEmail,
                    Email = seedEmail,
                    EmailConfirmed = true,
                    FirstName = seedFirst ?? "Site",
                    LastName = seedLast ?? "Manager",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user, seedPassword);
                if (!createResult.Succeeded)
                {
                    _logger.LogError("Failed to create seed manager user {Email}: {Errors}", seedEmail, string.Join(';', createResult.Errors));
                    return;
                }

                _logger.LogInformation("Created seed manager user {Email}", seedEmail);
            }

            // Ensure role membership for Manager
            const string managerRole = "Manager";
            if (!await _userManager.IsInRoleAsync(user, managerRole))
            {
                var addRoleResult = await _userManager.AddToRoleAsync(user, managerRole);
                if (!addRoleResult.Succeeded)
                {
                    _logger.LogError("Failed to add user {Email} to role {Role}: {Errors}", seedEmail, managerRole, string.Join(';', addRoleResult.Errors));
                    return;
                }
                _logger.LogInformation("Added user {Email} to role {Role}", seedEmail, managerRole);
            }
        }
    }
}

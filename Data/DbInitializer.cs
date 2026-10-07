using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data;

public static class DbInitializer
{
    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        // Verify database connectivity before attempting to seed
        if (!await context.Database.CanConnectAsync())
        {
            logger.LogWarning("Database is not reachable. Skipping role and admin seeding until database is available.");
            return;
        }

        // 1. Seed Mandatory Application Roles (Admin, Manager, SalesExecutive)
        string[] roles = [UserRoles.Admin, UserRoles.Manager, UserRoles.SalesExecutive];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                if (roleResult.Succeeded)
                {
                    logger.LogInformation("Role '{RoleName}' created successfully.", role);
                }
                else
                {
                    logger.LogError("Failed to create role '{RoleName}': {Errors}", 
                        role, 
                        string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
        }

        // 2. Seed Development Admin Account safely from configuration
        var adminEmail = configuration["SeedData:AdminUser:Email"] ?? "admin@acxiomcrm.local";
        var adminFullName = configuration["SeedData:AdminUser:FullName"] ?? "System Administrator";
        var adminPassword = configuration["SeedData:AdminUser:InitialPassword"];

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("Admin initial password is not configured in SeedData. Skipping admin creation.");
            return;
        }

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = adminFullName,
                EmailConfirmed = true,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            // ASP.NET Core Identity automatically applies adaptive PBKDF2 hashing
            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (createResult.Succeeded)
            {
                logger.LogInformation("Development Admin account '{AdminEmail}' created successfully.", adminEmail);

                var roleAssignResult = await userManager.AddToRoleAsync(adminUser, UserRoles.Admin);
                if (roleAssignResult.Succeeded)
                {
                    logger.LogInformation("Assigned role '{RoleName}' to user '{AdminEmail}'.", UserRoles.Admin, adminEmail);
                }
                else
                {
                    logger.LogError("Failed to assign '{RoleName}' role to '{AdminEmail}': {Errors}",
                        UserRoles.Admin,
                        adminEmail,
                        string.Join(", ", roleAssignResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogError("Failed to create Admin user '{AdminEmail}': {Errors}", 
                    adminEmail, 
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }
        }
    }
}

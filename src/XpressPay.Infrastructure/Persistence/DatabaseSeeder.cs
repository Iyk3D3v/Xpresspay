using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XpressPay.Domain.Constants;
using XpressPay.Infrastructure.Identity;

namespace XpressPay.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        try
        {
            await context.Database.EnsureCreatedAsync();

            foreach (var roleName in Roles.AllRoles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                    logger.LogInformation("Seeded role: {RoleName}", roleName);
                }
            }

            const string adminEmail = "admin@xpresspay.com";
            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
            if (existingAdmin == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = "admin",
                    Email = adminEmail,
                    FullName = "XpressPay Administrator",
                    EmailConfirmed = true
                };

                var createAdminResult = await userManager.CreateAsync(adminUser, "Admin@123456");
                if (createAdminResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, Roles.Admin);
                    await userManager.AddToRoleAsync(adminUser, Roles.User);
                    logger.LogInformation("Default admin user created: {AdminEmail}", adminEmail);
                }
                else
                {
                    logger.LogWarning("Failed to create default admin user: {Errors}", 
                        string.Join(", ", createAdminResult.Errors.Select(e => e.Description)));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }
}

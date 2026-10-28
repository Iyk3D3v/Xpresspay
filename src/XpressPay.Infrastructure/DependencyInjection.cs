using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XpressPay.Application.Common.Interfaces;
using XpressPay.Infrastructure.Identity;
using XpressPay.Infrastructure.Persistence;
using XpressPay.Infrastructure.Services;

namespace XpressPay.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? "Data Source=xpresspay.db";

        bool isSqlServer = connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                           connectionString.Contains("Data Source=tcp:", StringComparison.OrdinalIgnoreCase);

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (isSqlServer)
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOptimizedQueryService, OptimizedQueryService>();

        return services;
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XpressPay.Infrastructure.Persistence;
using XpressPay.WebApi;
using Xunit;

namespace XpressPay.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<WebApiProgram>
{
    private readonly string _dbName = $"xpresspay_test_{Guid.NewGuid():N}.db";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbName}");
        builder.UseSetting("Hangfire:UseInMemory", "true");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hangfire:UseInMemory"] = "true",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbName}"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_dbName}");
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_dbName)) File.Delete(_dbName);
            if (File.Exists($"{_dbName}-shm")) File.Delete($"{_dbName}-shm");
            if (File.Exists($"{_dbName}-wal")) File.Delete($"{_dbName}-wal");
        }
        catch
        {
        }
    }
}

[CollectionDefinition("IntegrationTests", DisableParallelization = true)]
public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
}

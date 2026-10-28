using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.Storage.SQLite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XpressPay.Application.Features.Reports;

namespace XpressPay.Infrastructure.BackgroundJobs;

public static class HangfireConfigurationExtensions
{
    public static IServiceCollection AddCustomHangfire(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useInMemoryStorage = false)
    {
        services.AddScoped<IFinancialReportJob, FinancialReportJob>();

        var isInMemory = useInMemoryStorage || configuration.GetValue<bool>("Hangfire:UseInMemory");

        services.AddHangfire((serviceProvider, config) =>
        {
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                  .UseSimpleAssemblyNameTypeSerializer()
                  .UseRecommendedSerializerSettings();

            if (isInMemory)
            {
                config.UseMemoryStorage();
            }
            else
            {
                var connectionString = configuration.GetConnectionString("HangfireConnection")
                                       ?? "Data Source=hangfire.db;";

                config.UseSQLiteStorage(connectionString, new SQLiteStorageOptions());
            }
        });

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Math.Max(2, Environment.ProcessorCount);
            options.ServerName = "XpressPay-BackgroundServer";
        });

        return services;
    }
}

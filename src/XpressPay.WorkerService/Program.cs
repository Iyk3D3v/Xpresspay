using Hangfire;
using XpressPay.Application;
using XpressPay.Application.Features.Reports;
using XpressPay.Infrastructure;
using XpressPay.Infrastructure.BackgroundJobs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCustomHangfire(builder.Configuration);

var app = builder.Build();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthorizationFilter() },
    DashboardTitle = "XpressPay Dedicated Background Worker Dashboard"
});

RecurringJob.AddOrUpdate<IFinancialReportJob>(
    "daily-financial-settlement-report",
    job => job.GenerateAndEmailDailySettlementReportAsync("admin@xpresspay.com", false, CancellationToken.None),
    Cron.Daily);

app.MapGet("/", () => Results.Ok(new
{
    service = "XpressPay Dedicated Background Worker Service",
    status = "Active & Processing Jobs",
    storage = "hangfire.db",
    dashboard = "/hangfire",
    recurringJob = "daily-financial-settlement-report (Cron.Daily)",
    serverTime = DateTime.UtcNow
}));

app.Run();

namespace XpressPay.WorkerService
{
    public partial class WorkerServiceProgram { }
}

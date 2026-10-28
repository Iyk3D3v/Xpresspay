using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using XpressPay.Application.Features.Reports;
using XpressPay.Infrastructure.BackgroundJobs;
using Xunit;

namespace XpressPay.Tests;

[Collection("IntegrationTests")]
public class FinancialReportJobTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public FinancialReportJobTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GenerateReportJob_EnqueuesAsynchronously_Returns202AcceptedWithJobId()
    {
        var request = new TriggerReportJobRequest
        {
            RecipientEmail = "settlements@xpresspay.com",
            SimulateFailure = false
        };

        var response = await _client.PostAsJsonAsync("/api/reports/generate-job", request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReportJobResponseDto>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.JobId));
        Assert.Equal("OnDemand (Fire-and-Forget)", body.ExecutionMode);
        Assert.Equal("Enqueued", body.Status);
        Assert.Equal("settlements@xpresspay.com", body.RecipientEmail);
        Assert.Equal("/hangfire", body.DashboardUrl);
    }

    [Fact]
    public async Task ScheduleReportJob_WithDelay_Returns202AcceptedWithScheduledAt()
    {
        var request = new TriggerReportJobRequest
        {
            RecipientEmail = "admin@xpresspay.com",
            DelaySeconds = 15,
            SimulateFailure = false
        };

        var response = await _client.PostAsJsonAsync("/api/reports/schedule-job", request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReportJobResponseDto>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.JobId));
        Assert.Equal("Delayed (Scheduled)", body.ExecutionMode);
        Assert.Equal("Scheduled", body.Status);
        Assert.Equal(15, body.DelaySeconds);
        Assert.NotNull(body.ScheduledAt);
        Assert.True(body.ScheduledAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task TriggerRecurringJob_Returns202Accepted()
    {
        var response = await _client.PostAsync("/api/reports/trigger-recurring", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task GetRecurringStatus_ReturnsConfiguredDailyCronSchedule()
    {
        var response = await _client.GetAsync("/api/reports/recurring-status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<RecurringJobStatusDto>();
        Assert.NotNull(status);
        Assert.Equal("daily-financial-settlement-report", status.JobId);
        Assert.Equal("0 0 * * *", status.CronExpression);
    }

    [Fact]
    public async Task PreviewSettlementStatement_CalculatesAggregatesOverSeededTransactions()
    {
        var response = await _client.GetAsync("/api/reports/preview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var statement = await response.Content.ReadFromJsonAsync<FinancialSettlementStatementDto>();
        Assert.NotNull(statement);
        Assert.True(statement.TotalTransactions >= 3000, $"Expected >= 3000 transactions, found {statement.TotalTransactions}");
        Assert.True(statement.CompletedTransactions > 0);
        Assert.True(statement.TotalVolume > 0);
        Assert.True(statement.TotalFees > 0);
        Assert.Equal(statement.TotalVolume - statement.TotalFees, statement.NetSettlement);
        Assert.True(statement.SuccessRatePercentage > 0.0 && statement.SuccessRatePercentage <= 100.0);
    }

    [Fact]
    public async Task FinancialReportJob_Resilience_ThrowsOnAttempt1AndSucceedsOnAttempt2()
    {
        using var scope = _factory.Services.CreateScope();
        var reportJob = scope.ServiceProvider.GetRequiredService<IFinancialReportJob>();
        var testRecipient = $"resilience_{Guid.NewGuid():N}@xpresspay.com";

        await Assert.ThrowsAsync<TransientEmailGatewayException>(async () =>
        {
            await reportJob.GenerateAndEmailDailySettlementReportAsync(
                recipientEmail: testRecipient,
                simulateTransientEmailFailure: true,
                ct: CancellationToken.None);
        });

        var exception = await Record.ExceptionAsync(async () =>
        {
            await reportJob.GenerateAndEmailDailySettlementReportAsync(
                recipientEmail: testRecipient,
                simulateTransientEmailFailure: true,
                ct: CancellationToken.None);
        });

        Assert.Null(exception);
    }
}

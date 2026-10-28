using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Mvc;
using XpressPay.Application.Features.Reports;

namespace XpressPay.WebApi.Controllers;

[ApiController]
[Route("api/reports")]
public class FinancialReportsController : ControllerBase
{
    private readonly IFinancialReportJob _reportJob;
    private readonly ILogger<FinancialReportsController> _logger;

    public FinancialReportsController(
        IFinancialReportJob reportJob,
        ILogger<FinancialReportsController> logger)
    {
        _reportJob = reportJob;
        _logger = logger;
    }

    [HttpPost("generate-job")]
    [ProducesResponseType(typeof(ReportJobResponseDto), StatusCodes.Status202Accepted)]
    public IActionResult EnqueueReportJob([FromBody] TriggerReportJobRequest? request)
    {
        request ??= new TriggerReportJobRequest();

        var jobId = BackgroundJob.Enqueue<IFinancialReportJob>(job =>
            job.GenerateAndEmailDailySettlementReportAsync(
                request.RecipientEmail,
                request.SimulateFailure,
                CancellationToken.None));

        _logger.LogInformation("Enqueued Financial Report Background Job {JobId} for recipient {Recipient} (SimulateFailure: {SimulateFailure})",
            jobId, request.RecipientEmail, request.SimulateFailure);

        var response = new ReportJobResponseDto
        {
            JobId = jobId,
            ExecutionMode = "OnDemand (Fire-and-Forget)",
            Status = "Enqueued",
            RecipientEmail = request.RecipientEmail,
            ScheduledAt = DateTimeOffset.UtcNow,
            DashboardUrl = "/hangfire",
            Message = request.SimulateFailure
                ? $"Job {jobId} enqueued with simulated SMTP timeout. Inspect '/hangfire' under 'Retries' to observe automatic retries."
                : $"Job {jobId} enqueued for immediate asynchronous processing. Inspect progress at '/hangfire'."
        };

        return Accepted(response);
    }

    [HttpPost("schedule-job")]
    [ProducesResponseType(typeof(ReportJobResponseDto), StatusCodes.Status202Accepted)]
    public IActionResult ScheduleReportJob([FromBody] TriggerReportJobRequest? request)
    {
        request ??= new TriggerReportJobRequest();
        var delaySeconds = request.DelaySeconds is > 0 ? request.DelaySeconds.Value : 10;
        var scheduledTime = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);

        var jobId = BackgroundJob.Schedule<IFinancialReportJob>(
            job => job.GenerateAndEmailDailySettlementReportAsync(
                request.RecipientEmail,
                request.SimulateFailure,
                CancellationToken.None),
            TimeSpan.FromSeconds(delaySeconds));

        _logger.LogInformation("Scheduled Financial Report Job {JobId} with delay {Delay}s for {Recipient}",
            jobId, delaySeconds, request.RecipientEmail);

        var response = new ReportJobResponseDto
        {
            JobId = jobId,
            ExecutionMode = "Delayed (Scheduled)",
            Status = "Scheduled",
            RecipientEmail = request.RecipientEmail,
            DelaySeconds = delaySeconds,
            ScheduledAt = scheduledTime,
            DashboardUrl = "/hangfire",
            Message = $"Job {jobId} scheduled to execute in {delaySeconds} seconds ({scheduledTime:HH:mm:ss} UTC). View countdown at '/hangfire'."
        };

        return Accepted(response);
    }

    [HttpPost("trigger-recurring")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult TriggerRecurringJob()
    {
        const string recurringJobId = "daily-financial-settlement-report";
        RecurringJob.TriggerJob(recurringJobId);

        _logger.LogInformation("Manually triggered recurring job {JobId}", recurringJobId);

        return Accepted(new
        {
            JobId = recurringJobId,
            Message = "Recurring job triggered successfully. A new execution instance has been enqueued in Hangfire.",
            DashboardUrl = "/hangfire"
        });
    }

    [HttpGet("recurring-status")]
    public IActionResult GetRecurringStatus()
    {
        using var connection = JobStorage.Current.GetConnection();
        var recurringJobs = connection.GetRecurringJobs();
        var dailyJob = recurringJobs.FirstOrDefault(j => j.Id == "daily-financial-settlement-report");

        if (dailyJob == null)
        {
            return Ok(new
            {
                Status = "Not registered or storage initializing",
                DashboardUrl = "/hangfire"
            });
        }

        return Ok(new RecurringJobStatusDto
        {
            JobId = dailyJob.Id,
            CronExpression = dailyJob.Cron,
            Queue = dailyJob.Queue ?? "default",
            NextExecution = dailyJob.NextExecution,
            LastExecution = dailyJob.LastExecution,
            TargetMethod = $"{dailyJob.Job.Type.Name}.{dailyJob.Job.Method.Name}"
        });
    }

    [HttpGet("preview")]
    public async Task<IActionResult> PreviewSettlementStatement(CancellationToken ct)
    {
        var statement = await _reportJob.GenerateSettlementSummaryAsync(ct);
        return Ok(statement);
    }
}

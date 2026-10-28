namespace XpressPay.Application.Features.Reports;

public class TriggerReportJobRequest
{
    public string RecipientEmail { get; set; } = "admin@xpresspay.com";

    public int? DelaySeconds { get; set; }

    public bool SimulateFailure { get; set; } = false;
}

public class ReportJobResponseDto
{
    public string JobId { get; set; } = string.Empty;
    public string ExecutionMode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public DateTimeOffset? ScheduledAt { get; set; }
    public int? DelaySeconds { get; set; }
    public string DashboardUrl { get; set; } = "/hangfire";
    public string Message { get; set; } = string.Empty;
}

public class FinancialSettlementStatementDto
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string ReportPeriod { get; set; } = "Last 24 Hours";
    public int TotalTransactions { get; set; }
    public int CompletedTransactions { get; set; }
    public int FailedTransactions { get; set; }
    public int PendingTransactions { get; set; }
    public int RefundedTransactions { get; set; }
    public decimal TotalVolume { get; set; }
    public decimal TotalFees { get; set; }
    public decimal NetSettlement { get; set; }
    public double SuccessRatePercentage { get; set; }
}

public class RecurringJobStatusDto
{
    public string JobId { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string Queue { get; set; } = "default";
    public DateTime? NextExecution { get; set; }
    public DateTime? LastExecution { get; set; }
    public string TargetMethod { get; set; } = string.Empty;
}

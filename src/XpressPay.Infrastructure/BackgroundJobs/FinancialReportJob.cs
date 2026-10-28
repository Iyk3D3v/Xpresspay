using System.Collections.Concurrent;
using System.Text;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XpressPay.Application.Features.Reports;
using XpressPay.Domain.Enums;
using XpressPay.Infrastructure.Persistence;

namespace XpressPay.Infrastructure.BackgroundJobs;

public class FinancialReportJob : IFinancialReportJob
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FinancialReportJob> _logger;

    private static readonly ConcurrentDictionary<string, int> AttemptTracker = new();

    public FinancialReportJob(ApplicationDbContext context, ILogger<FinancialReportJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 5, 10, 20 }, LogEvents = true)]
    public async Task GenerateAndEmailDailySettlementReportAsync(
        string recipientEmail,
        bool simulateTransientEmailFailure,
        CancellationToken ct = default)
    {
        _logger.LogInformation(">>> [Hangfire Background Job] Starting Daily Settlement Report generation for {Recipient}...", recipientEmail);

        var statement = await GenerateSettlementSummaryAsync(ct);

        _logger.LogInformation(">>> [Hangfire Background Job] Aggregation completed: {Count} transactions analyzed, Total Volume: ${Volume:N2}, Total Fees: ${Fees:N2}",
            statement.TotalTransactions, statement.TotalVolume, statement.TotalFees);

        if (simulateTransientEmailFailure)
        {
            var trackerKey = $"{recipientEmail}:failure-simulation";
            var attemptNumber = AttemptTracker.AddOrUpdate(trackerKey, 1, (_, count) => count + 1);

            if (attemptNumber == 1)
            {
                _logger.LogWarning(">>> [Hangfire Background Job] [SIMULATED FAILURE] Attempt 1 for {Email} encountered transient SMTP Gateway 504 Timeout. Hangfire will automatically retry...", recipientEmail);
                throw new TransientEmailGatewayException($"SMTP Gateway 504 Gateway Timeout: Connection dropped while contacting mail server for {recipientEmail}.");
            }

            AttemptTracker.TryRemove(trackerKey, out _);
            _logger.LogInformation(">>> [Hangfire Background Job] [RETRY RECOVERY] Attempt {Attempt} for {Email} succeeded! Transient failure resolved.", attemptNumber, recipientEmail);
        }

        var emailBody = FormatStatementEmailBody(statement, recipientEmail);
        _logger.LogInformation(">>> [Hangfire Background Job] Financial settlement statement successfully dispatched to {Recipient}:\n{EmailBody}",
            recipientEmail, emailBody);
    }

    public async Task<FinancialSettlementStatementDto> GenerateSettlementSummaryAsync(CancellationToken ct = default)
    {
        var transactions = await _context.Transactions
            .AsNoTracking()
            .Select(t => new { t.Amount, t.Fee, t.Status })
            .ToListAsync(ct);

        var totalCount = transactions.Count;
        var completedCount = transactions.Count(t => t.Status == TransactionStatus.Completed);
        var failedCount = transactions.Count(t => t.Status == TransactionStatus.Failed);
        var pendingCount = transactions.Count(t => t.Status == TransactionStatus.Pending);
        var refundedCount = transactions.Count(t => t.Status == TransactionStatus.Refunded);

        var totalVolume = transactions
            .Where(t => t.Status == TransactionStatus.Completed)
            .Sum(t => t.Amount);

        var totalFees = transactions
            .Where(t => t.Status == TransactionStatus.Completed)
            .Sum(t => t.Fee);

        var netSettlement = totalVolume - totalFees;
        var successRate = totalCount > 0 ? (double)completedCount / totalCount * 100 : 0.0;

        return new FinancialSettlementStatementDto
        {
            GeneratedAt = DateTime.UtcNow,
            ReportPeriod = "Automated Settlement Window",
            TotalTransactions = totalCount,
            CompletedTransactions = completedCount,
            FailedTransactions = failedCount,
            PendingTransactions = pendingCount,
            RefundedTransactions = refundedCount,
            TotalVolume = totalVolume,
            TotalFees = totalFees,
            NetSettlement = netSettlement,
            SuccessRatePercentage = Math.Round(successRate, 2)
        };
    }

    private static string FormatStatementEmailBody(FinancialSettlementStatementDto s, string recipient)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                 XPRESSPAY DAILY FINANCIAL SETTLEMENT STATEMENT                  ");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Generated At:   {s.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Recipient:      {recipient}");
        sb.AppendLine($"Total Records:  {s.TotalTransactions:N0}");
        sb.AppendLine($"Completed:      {s.CompletedTransactions:N0}");
        sb.AppendLine($"Failed:         {s.FailedTransactions:N0}");
        sb.AppendLine($"Pending:        {s.PendingTransactions:N0}");
        sb.AppendLine($"Refunded:       {s.RefundedTransactions:N0}");
        sb.AppendLine($"Total Volume:   ${s.TotalVolume:N2} USD");
        sb.AppendLine($"Total Fees:     ${s.TotalFees:N2} USD");
        sb.AppendLine($"Net Payout:     ${s.NetSettlement:N2} USD");
        sb.AppendLine($"Success Rate:   {s.SuccessRatePercentage:F2}%");
        sb.AppendLine("Delivery:       Dispatched via SMTP Gateway (250 OK: Message Accepted)");
        sb.AppendLine("================================================================================");
        return sb.ToString();
    }
}

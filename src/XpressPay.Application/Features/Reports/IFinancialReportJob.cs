namespace XpressPay.Application.Features.Reports;

public interface IFinancialReportJob
{
    Task GenerateAndEmailDailySettlementReportAsync(
        string recipientEmail,
        bool simulateTransientEmailFailure,
        CancellationToken ct = default);

    Task<FinancialSettlementStatementDto> GenerateSettlementSummaryAsync(CancellationToken ct = default);
}

using XpressPay.Application.DTOs;

namespace XpressPay.Application.Common.Interfaces;

public interface IOptimizedQueryService
{
    Task<List<TransactionSummaryDto>> GetCustomerRecentTransactionsAsync(
        Guid customerId, 
        CancellationToken ct = default);

    Task<CustomerDetailsDto?> GetCustomerWithTransactionsSplitAsync(
        Guid customerId, 
        CancellationToken ct = default);

    Task<KeysetPagedResultDto<TransactionSummaryDto>> GetTransactionsKeysetAsync(
        DateTime? lastCreatedAt, 
        Guid? lastId, 
        int pageSize = 20, 
        CancellationToken ct = default);

    Task<List<DailyTransactionAggregateDto>> GetDailyTransactionAggregatesAsync(
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken ct = default);

    Task<bool> HasActiveTransactionsAsync(
        Guid customerId, 
        CancellationToken ct = default);

    Task<List<TransactionSummaryDto>> GetTransactionsByDateRangeAsync(
        DateTime fromDate, 
        DateTime toDate, 
        CancellationToken ct = default);

    Task<int> ExpireStalePendingTransactionsAsync(
        DateTime cutoffTime, 
        CancellationToken ct = default);

    Task<TransactionSummaryDto?> GetByReferenceFastAsync(
        string referenceNumber, 
        CancellationToken ct = default);
}

using Microsoft.EntityFrameworkCore;
using XpressPay.Application.Common.Interfaces;
using XpressPay.Application.DTOs;
using XpressPay.Domain.Entities;
using XpressPay.Domain.Enums;
using XpressPay.Infrastructure.Persistence;

namespace XpressPay.Infrastructure.Services;

public class OptimizedQueryService : IOptimizedQueryService
{
    private readonly ApplicationDbContext _context;

    private static readonly Func<ApplicationDbContext, string, Task<TransactionSummaryDto?>> GetTransactionByRefCompiled =
        EF.CompileAsyncQuery((ApplicationDbContext context, string referenceNumber) =>
            context.Transactions
                .AsNoTracking()
                .Where(t => t.ReferenceNumber == referenceNumber)
                .Select(t => new TransactionSummaryDto
                {
                    Id = t.Id,
                    ReferenceNumber = t.ReferenceNumber,
                    Amount = t.Amount,
                    Fee = t.Fee,
                    Currency = t.Currency,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt
                })
                .FirstOrDefault()
        );

    public OptimizedQueryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TransactionSummaryDto>> GetCustomerRecentTransactionsAsync(
        Guid customerId, 
        CancellationToken ct = default)
    {
        return await _context.Transactions
            .AsNoTracking()
            .Where(t => t.CustomerId == customerId && t.Status == TransactionStatus.Completed)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new TransactionSummaryDto
            {
                Id = t.Id,
                ReferenceNumber = t.ReferenceNumber,
                Amount = t.Amount,
                Fee = t.Fee,
                Currency = t.Currency,
                Status = t.Status,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<CustomerDetailsDto?> GetCustomerWithTransactionsSplitAsync(
        Guid customerId, 
        CancellationToken ct = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new CustomerDetailsDto
            {
                Id = c.Id,
                Name = c.Name,
                Email = c.Email,
                RecentTransactions = c.Transactions
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(20)
                    .Select(t => new TransactionSummaryDto
                    {
                        Id = t.Id,
                        ReferenceNumber = t.ReferenceNumber,
                        Amount = t.Amount,
                        Fee = t.Fee,
                        Currency = t.Currency,
                        Status = t.Status,
                        CreatedAt = t.CreatedAt
                    })
                    .ToList()
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<KeysetPagedResultDto<TransactionSummaryDto>> GetTransactionsKeysetAsync(
        DateTime? lastCreatedAt, 
        Guid? lastId, 
        int pageSize = 20, 
        CancellationToken ct = default)
    {
        IQueryable<Transaction> query = _context.Transactions
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id);

        if (lastCreatedAt.HasValue && lastId.HasValue)
        {
            query = query.Where(t => 
                t.CreatedAt < lastCreatedAt.Value || 
                (t.CreatedAt == lastCreatedAt.Value && t.Id.CompareTo(lastId.Value) < 0));
        }

        var items = await query
            .Take(pageSize + 1)
            .Select(t => new TransactionSummaryDto
            {
                Id = t.Id,
                ReferenceNumber = t.ReferenceNumber,
                Amount = t.Amount,
                Fee = t.Fee,
                Currency = t.Currency,
                Status = t.Status,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(ct);

        bool hasMore = items.Count > pageSize;
        if (hasMore)
        {
            items.RemoveAt(pageSize);
        }

        var lastItem = items.LastOrDefault();

        return new KeysetPagedResultDto<TransactionSummaryDto>
        {
            Items = items,
            HasMore = hasMore,
            PageSize = pageSize,
            NextLastCreatedAt = lastItem?.CreatedAt,
            NextLastId = lastItem?.Id
        };
    }

    public async Task<List<DailyTransactionAggregateDto>> GetDailyTransactionAggregatesAsync(
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken ct = default)
    {
        return await _context.Transactions
            .AsNoTracking()
            .Where(t => t.CreatedAt >= startDate && t.CreatedAt < endDate)
            .GroupBy(t => new { Date = t.CreatedAt.Date, t.Status })
            .Select(g => new DailyTransactionAggregateDto
            {
                Date = g.Key.Date,
                Status = g.Key.Status.ToString(),
                TotalCount = g.Count(),
                TotalVolume = g.Sum(t => t.Amount),
                AverageAmount = g.Average(t => t.Amount),
                TotalFees = g.Sum(t => t.Fee)
            })
            .OrderByDescending(r => r.Date)
            .ToListAsync(ct);
    }

    public async Task<bool> HasActiveTransactionsAsync(
        Guid customerId, 
        CancellationToken ct = default)
    {
        return await _context.Transactions
            .AsNoTracking()
            .AnyAsync(t => t.CustomerId == customerId && t.Status == TransactionStatus.Pending, ct);
    }

    public async Task<List<TransactionSummaryDto>> GetTransactionsByDateRangeAsync(
        DateTime fromDate, 
        DateTime toDate, 
        CancellationToken ct = default)
    {
        return await _context.Transactions
            .AsNoTracking()
            .Where(t => t.CreatedAt >= fromDate && t.CreatedAt < toDate)
            .OrderByDescending(t => t.CreatedAt)
            .Take(100)
            .Select(t => new TransactionSummaryDto
            {
                Id = t.Id,
                ReferenceNumber = t.ReferenceNumber,
                Amount = t.Amount,
                Fee = t.Fee,
                Currency = t.Currency,
                Status = t.Status,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<int> ExpireStalePendingTransactionsAsync(
        DateTime cutoffTime, 
        CancellationToken ct = default)
    {
        return await _context.Transactions
            .Where(t => t.Status == TransactionStatus.Pending && t.CreatedAt < cutoffTime)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(t => t.Status, TransactionStatus.Failed)
                .SetProperty(t => t.Description, "Expired automatically due to timeout"), 
                ct);
    }

    public async Task<TransactionSummaryDto?> GetByReferenceFastAsync(
        string referenceNumber, 
        CancellationToken ct = default)
    {
        return await GetTransactionByRefCompiled(_context, referenceNumber);
    }
}

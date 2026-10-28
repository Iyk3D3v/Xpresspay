using Microsoft.AspNetCore.Mvc;
using XpressPay.Application.Common.Interfaces;
using XpressPay.Application.DTOs;
using XpressPay.Infrastructure.Persistence;

namespace XpressPay.WebApi.Controllers;

[ApiController]
[Route("api/optimized-queries")]
public class OptimizedQueriesController : ControllerBase
{
    private readonly IOptimizedQueryService _queryService;

    public OptimizedQueriesController(IOptimizedQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet("test-metadata")]
    public IActionResult GetTestMetadata()
    {
        return Ok(new
        {
            Message = "Use these pre-seeded IDs to test the query optimization endpoints.",
            KnownCustomer1 = OptimizationDataSeeder.KnownCustomerId1,
            KnownCustomer2 = OptimizationDataSeeder.KnownCustomerId2,
            KnownReferenceNumber = OptimizationDataSeeder.KnownReferenceNumber
        });
    }

    [HttpGet("customer-summary/{customerId:guid}")]
    public async Task<IActionResult> GetCustomerRecentTransactions(
        [FromRoute] Guid customerId, 
        CancellationToken ct)
    {
        var result = await _queryService.GetCustomerRecentTransactionsAsync(customerId, ct);
        return Ok(new
        {
            Optimization = "Scenario 1: Selective Projection & AsNoTracking",
            Explanation = "Bypasses ChangeTracker entity tracking and fetches only selected columns (avoids SELECT *).",
            Count = result.Count,
            Data = result
        });
    }

    [HttpGet("customer-details-split/{customerId:guid}")]
    public async Task<IActionResult> GetCustomerWithTransactionsSplit(
        [FromRoute] Guid customerId, 
        CancellationToken ct)
    {
        var result = await _queryService.GetCustomerWithTransactionsSplitAsync(customerId, ct);
        if (result == null) return NotFound("Customer not found.");

        return Ok(new
        {
            Optimization = "Scenario 2: Split Query (AsSplitQuery)",
            Explanation = "Splits parent and child collection queries into 2 lean SQL statements, preventing Cartesian explosion.",
            Data = result
        });
    }

    [HttpGet("transactions-keyset")]
    public async Task<IActionResult> GetTransactionsKeyset(
        [FromQuery] DateTime? lastCreatedAt,
        [FromQuery] Guid? lastId,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _queryService.GetTransactionsKeysetAsync(lastCreatedAt, lastId, pageSize, ct);
        return Ok(new
        {
            Optimization = "Scenario 3: Keyset / Seek Pagination",
            Explanation = "Seeks directly to next key range using B-tree index; eliminates deep page latency spikes.",
            PageSize = pageSize,
            HasMore = result.HasMore,
            NextCursor = new
            {
                LastCreatedAt = result.NextLastCreatedAt,
                LastId = result.NextLastId
            },
            ItemsReturned = result.Items.Count,
            Data = result.Items
        });
    }

    [HttpGet("daily-aggregates")]
    public async Task<IActionResult> GetDailyAggregates(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken ct = default)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;

        var result = await _queryService.GetDailyTransactionAggregatesAsync(start, end, ct);
        return Ok(new
        {
            Optimization = "Scenario 4: Server-Side Grouping & Aggregations",
            Explanation = "Executes aggregate functions inside SQL engine. Transfers only calculated summary rows.",
            DateRange = new { Start = start, End = end },
            TotalSummaryRows = result.Count,
            Data = result
        });
    }

    [HttpGet("check-active/{customerId:guid}")]
    public async Task<IActionResult> CheckActiveTransactions(
        [FromRoute] Guid customerId, 
        CancellationToken ct)
    {
        var hasActive = await _queryService.HasActiveTransactionsAsync(customerId, ct);
        return Ok(new
        {
            Optimization = "Scenario 5a: SARGable Existence Check (AnyAsync)",
            Explanation = "Translates to SQL EXISTS(...), stopping search immediately on 1st matching row.",
            CustomerId = customerId,
            HasActivePendingTransactions = hasActive
        });
    }

    [HttpGet("by-date-range")]
    public async Task<IActionResult> GetByDateRange(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct = default)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-7);
        var to = toDate ?? DateTime.UtcNow;

        var result = await _queryService.GetTransactionsByDateRangeAsync(from, to, ct);
        return Ok(new
        {
            Optimization = "Scenario 5b: SARGable Range Query",
            Explanation = "Preserves index seek capabilities by avoiding non-SARGable column functions.",
            From = from,
            To = to,
            Count = result.Count,
            Data = result
        });
    }

    [HttpPost("expire-stale")]
    public async Task<IActionResult> ExpireStaleTransactions(
        [FromQuery] int hoursAgo = 24, 
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-hoursAgo);
        var updatedCount = await _queryService.ExpireStalePendingTransactionsAsync(cutoff, ct);

        return Ok(new
        {
            Optimization = "Scenario 6: Direct Bulk Updates (ExecuteUpdateAsync)",
            Explanation = "Executes single UPDATE statement in database. 0 entities allocated or tracked in memory.",
            CutoffTime = cutoff,
            TransactionsExpired = updatedCount
        });
    }

    [HttpGet("by-reference/{referenceNumber}")]
    public async Task<IActionResult> GetByReferenceFast(
        [FromRoute] string referenceNumber, 
        CancellationToken ct)
    {
        var result = await _queryService.GetByReferenceFastAsync(referenceNumber, ct);
        if (result == null) return NotFound("Transaction reference not found.");

        return Ok(new
        {
            Optimization = "Scenario 7: Pre-compiled Query (EF.CompileAsyncQuery)",
            Explanation = "Reuses pre-compiled query delegate, eliminating expression tree parsing overhead.",
            Data = result
        });
    }
}

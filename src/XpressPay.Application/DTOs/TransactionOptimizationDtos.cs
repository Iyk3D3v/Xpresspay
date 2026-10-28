using XpressPay.Domain.Enums;

namespace XpressPay.Application.DTOs;

public class TransactionSummaryDto
{
    public Guid Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public string Currency { get; set; } = string.Empty;
    public TransactionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerDetailsDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<TransactionSummaryDto> RecentTransactions { get; set; } = new();
}

public class DailyTransactionAggregateDto
{
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public decimal TotalVolume { get; set; }
    public decimal AverageAmount { get; set; }
    public decimal TotalFees { get; set; }
}

public class KeysetPaginationRequestDto
{
    public DateTime? LastCreatedAt { get; set; }
    public Guid? LastId { get; set; }
    public int PageSize { get; set; } = 20;
}

public class KeysetPagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public DateTime? NextLastCreatedAt { get; set; }
    public Guid? NextLastId { get; set; }
    public bool HasMore { get; set; }
    public int PageSize { get; set; }
}

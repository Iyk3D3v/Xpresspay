using XpressPay.Domain.Enums;

namespace XpressPay.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public string Currency { get; set; } = "USD";
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string PaymentMethod { get; set; } = "Card";
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

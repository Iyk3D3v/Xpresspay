namespace XpressPay.WalletService.Domain;

public class WalletTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WalletId { get; set; }
    public Wallet? Wallet { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = "Debit";
    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
}

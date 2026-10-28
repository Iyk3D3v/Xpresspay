namespace XpressPay.WalletService.Domain;

public class Wallet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccountHolder { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "NGN";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<WalletTransaction> Transactions { get; set; } = new();
}

using System.ComponentModel.DataAnnotations;

namespace XpressPay.Application.Features.Checkouts;

public class CheckoutRequestDto
{
    [Required(ErrorMessage = "CustomerAccountId is required.")]
    public Guid CustomerAccountId { get; set; }

    [Required]
    [Range(0.01, 10000000.0, ErrorMessage = "Checkout amount must be strictly greater than 0.00 NGN.")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "ItemDescription must be between 2 and 200 characters.")]
    public string ItemDescription { get; set; } = "Online Store Purchase";

    [Required]
    [RegularExpression("^NGN$", ErrorMessage = "Currency must be NGN.")]
    public string Currency { get; set; } = "NGN";
}

public class CheckoutResponseDto
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = "Completed";
    public string ReferenceNumber { get; set; } = string.Empty;
    public Guid CustomerAccountId { get; set; }
    public string ItemDescription { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public string Currency { get; set; } = "NGN";
    public decimal RemainingWalletBalance { get; set; }
    public Guid DebitTransactionId { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = string.Empty;
}

public class DebitWalletCommand
{
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class WalletDebitResult
{
    public bool Success { get; set; }
    public Guid DebitId { get; set; }
    public Guid AccountId { get; set; }
    public decimal PreviousBalance { get; set; }
    public decimal DebitedAmount { get; set; }
    public decimal NewBalance { get; set; }
    public string Currency { get; set; } = "NGN";
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class WalletAccountSummaryDto
{
    public Guid AccountId { get; set; }
    public string AccountHolder { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "NGN";
    public bool IsActive { get; set; }
    public DateTime LastUpdated { get; set; }
}

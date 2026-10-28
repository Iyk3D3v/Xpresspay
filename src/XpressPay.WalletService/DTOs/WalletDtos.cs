using System.ComponentModel.DataAnnotations;

namespace XpressPay.WalletService.DTOs;

public class DebitWalletRequest
{
    [Required(ErrorMessage = "AccountId is required.")]
    public Guid AccountId { get; set; }

    [Required(ErrorMessage = "Debit amount is required.")]
    [Range(0.01, 10000000.0, ErrorMessage = "Amount must be strictly greater than 0.00 NGN.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Currency is required.")]
    [RegularExpression("^NGN$", ErrorMessage = "Currency must be NGN.")]
    public string Currency { get; set; } = "NGN";

    [Required(ErrorMessage = "ReferenceNumber is required.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "ReferenceNumber must be between 3 and 100 characters.")]
    public string ReferenceNumber { get; set; } = string.Empty;

    public string Description { get; set; } = "Checkout payment debit";
}

public class CreditWalletRequest
{
    [Required(ErrorMessage = "AccountId is required.")]
    public Guid AccountId { get; set; }

    [Required]
    [Range(0.01, 10000000.0, ErrorMessage = "Credit amount must be greater than 0.00 NGN.")]
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "NGN";
    public string Description { get; set; } = "Wallet deposit / credit top-up";
}

public class DebitWalletResultDto
{
    public bool Success { get; set; }
    public Guid DebitId { get; set; }
    public Guid AccountId { get; set; }
    public decimal PreviousBalance { get; set; }
    public decimal DebitedAmount { get; set; }
    public decimal NewBalance { get; set; }
    public string Currency { get; set; } = "NGN";
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = string.Empty;
}

public class WalletSummaryDto
{
    public Guid AccountId { get; set; }
    public string AccountHolder { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "NGN";
    public bool IsActive { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class InsufficientFundsErrorResponse
{
    public string Error { get; set; } = "InsufficientFunds";
    public string Message { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal Shortfall { get; set; }
    public string Currency { get; set; } = "NGN";
}

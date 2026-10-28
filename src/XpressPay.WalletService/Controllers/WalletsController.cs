using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XpressPay.WalletService.Domain;
using XpressPay.WalletService.DTOs;
using XpressPay.WalletService.Persistence;
using XpressPay.WalletService.Security;

namespace XpressPay.WalletService.Controllers;

[ApiController]
[Route("api/wallets")]
public class WalletsController : ControllerBase
{
    private readonly WalletDbContext _context;
    private readonly ILogger<WalletsController> _logger;

    public WalletsController(WalletDbContext context, ILogger<WalletsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("demo-accounts")]
    public async Task<IActionResult> GetDemoAccounts()
    {
        var accounts = await _context.Wallets
            .AsNoTracking()
            .Select(w => new WalletSummaryDto
            {
                AccountId = w.Id,
                AccountHolder = w.AccountHolder,
                Email = w.Email,
                Balance = w.Balance,
                Currency = w.Currency,
                IsActive = w.IsActive,
                LastUpdated = w.UpdatedAt
            })
            .ToListAsync();

        return Ok(accounts);
    }

    [HttpGet("{accountId:guid}")]
    public async Task<IActionResult> GetWallet(Guid accountId)
    {
        var wallet = await _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == accountId);

        if (wallet == null)
        {
            return NotFound(new
            {
                statusCode = StatusCodes.Status404NotFound,
                error = "WalletNotFound",
                message = $"Wallet account '{accountId}' was not found in the wallet database."
            });
        }

        return Ok(new WalletSummaryDto
        {
            AccountId = wallet.Id,
            AccountHolder = wallet.AccountHolder,
            Email = wallet.Email,
            Balance = wallet.Balance,
            Currency = wallet.Currency,
            IsActive = wallet.IsActive,
            LastUpdated = wallet.UpdatedAt
        });
    }

    [HttpPost("debit")]
    [ServiceAuth]
    [ProducesResponseType(typeof(DebitWalletResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(InsufficientFundsErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DebitWallet([FromBody] DebitWalletRequest request)
    {
        _logger.LogInformation("Processing inter-service debit request for Account {AccountId}, Amount: ₦{Amount:N2} {Currency}, Ref: {Ref}",
            request.AccountId, request.Amount, request.Currency, request.ReferenceNumber);

        if (!string.Equals(request.Currency, "NGN", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                statusCode = StatusCodes.Status400BadRequest,
                error = "UnsupportedCurrency",
                message = $"Unsupported currency '{request.Currency}'. Only 'NGN' (Nigerian Naira) is supported."
            });
        }

        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == request.AccountId);
        if (wallet == null)
        {
            _logger.LogWarning("Debit failed: Account {AccountId} not found", request.AccountId);
            return NotFound(new
            {
                statusCode = StatusCodes.Status404NotFound,
                error = "WalletNotFound",
                message = $"Wallet account '{request.AccountId}' does not exist in the wallet database."
            });
        }

        if (!wallet.IsActive)
        {
            return BadRequest(new
            {
                statusCode = StatusCodes.Status400BadRequest,
                error = "AccountInactive",
                message = $"Wallet account '{request.AccountId}' is suspended or inactive."
            });
        }

        if (wallet.Balance < request.Amount)
        {
            var shortfall = request.Amount - wallet.Balance;
            _logger.LogWarning("Debit rejected for {AccountId}: Insufficient balance. Available: ₦{Avail:N2}, Requested: ₦{Req:N2}, Shortfall: ₦{Shortfall:N2}",
                wallet.Id, wallet.Balance, request.Amount, shortfall);

            return UnprocessableEntity(new InsufficientFundsErrorResponse
            {
                Error = "InsufficientFunds",
                Message = $"Insufficient wallet balance. Current balance is ₦{wallet.Balance:N2} NGN, but ₦{request.Amount:N2} NGN was requested.",
                CurrentBalance = wallet.Balance,
                RequestedAmount = request.Amount,
                Shortfall = shortfall,
                Currency = "NGN"
            });
        }

        var balanceBefore = wallet.Balance;
        wallet.Balance -= request.Amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        var transaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = request.Amount,
            Type = "Debit",
            ReferenceNumber = request.ReferenceNumber,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = request.Description,
            Timestamp = DateTime.UtcNow
        };

        _context.WalletTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Debit successful for {AccountId}. Old Balance: ₦{Old:N2}, New Balance: ₦{New:N2}",
            wallet.Id, balanceBefore, wallet.Balance);

        return Ok(new DebitWalletResultDto
        {
            Success = true,
            DebitId = transaction.Id,
            AccountId = wallet.Id,
            PreviousBalance = balanceBefore,
            DebitedAmount = request.Amount,
            NewBalance = wallet.Balance,
            Currency = "NGN",
            ReferenceNumber = request.ReferenceNumber,
            ProcessedAt = transaction.Timestamp,
            Message = $"Successfully debited ₦{request.Amount:N2} NGN from {wallet.AccountHolder}'s wallet. New balance: ₦{wallet.Balance:N2} NGN."
        });
    }

    [HttpPost("credit")]
    [ServiceAuth]
    public async Task<IActionResult> CreditWallet([FromBody] CreditWalletRequest request)
    {
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == request.AccountId);
        if (wallet == null)
        {
            return NotFound(new { error = "WalletNotFound", message = $"Account '{request.AccountId}' not found." });
        }

        var balanceBefore = wallet.Balance;
        wallet.Balance += request.Amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        var transaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = request.Amount,
            Type = "Credit",
            ReferenceNumber = $"DEP-{Guid.NewGuid():N}"[..12].ToUpper(),
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = request.Description,
            Timestamp = DateTime.UtcNow
        };

        _context.WalletTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            AccountId = wallet.Id,
            PreviousBalance = balanceBefore,
            CreditedAmount = request.Amount,
            NewBalance = wallet.Balance,
            Currency = "NGN",
            Message = $"Successfully credited ₦{request.Amount:N2} NGN to {wallet.AccountHolder}'s wallet."
        });
    }
}

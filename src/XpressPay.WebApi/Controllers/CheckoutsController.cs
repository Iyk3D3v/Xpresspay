using Microsoft.AspNetCore.Mvc;
using XpressPay.Application.Features.Checkouts;
using XpressPay.Domain.Entities;
using XpressPay.Domain.Enums;
using XpressPay.Infrastructure.Persistence;

namespace XpressPay.WebApi.Controllers;

[ApiController]
[Route("api/checkouts")]
public class CheckoutsController : ControllerBase
{
    private readonly IWalletServiceClient _walletClient;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CheckoutsController> _logger;

    public CheckoutsController(
        IWalletServiceClient walletClient,
        ApplicationDbContext context,
        ILogger<CheckoutsController> logger)
    {
        _walletClient = walletClient;
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CheckoutResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ProcessCheckout([FromBody] CheckoutRequestDto request, CancellationToken ct)
    {
        _logger.LogInformation("Initiating checkout order for Account {AccountId}, Amount: ₦{Amount:N2} {Currency}, Item: {Item}",
            request.CustomerAccountId, request.Amount, request.Currency, request.ItemDescription);

        if (!string.Equals(request.Currency, "NGN", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                statusCode = StatusCodes.Status400BadRequest,
                error = "UnsupportedCurrency",
                message = $"Unsupported currency '{request.Currency}'. Only 'NGN' (Nigerian Naira) is supported for checkouts."
            });
        }

        var referenceNumber = $"CHK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpper();

        var debitCommand = new DebitWalletCommand
        {
            AccountId = request.CustomerAccountId,
            Amount = request.Amount,
            Currency = "NGN",
            ReferenceNumber = referenceNumber,
            Description = $"Checkout payment for: {request.ItemDescription}"
        };

        var debitResult = await _walletClient.DebitWalletAsync(debitCommand, ct);

        if (!debitResult.Success)
        {
            return debitResult.StatusCode switch
            {
                StatusCodes.Status422UnprocessableEntity => UnprocessableEntity(new
                {
                    statusCode = StatusCodes.Status422UnprocessableEntity,
                    error = "InsufficientWalletBalance",
                    message = debitResult.ErrorMessage,
                    requestedAmount = request.Amount,
                    currency = "NGN"
                }),
                StatusCodes.Status404NotFound => NotFound(new
                {
                    statusCode = StatusCodes.Status404NotFound,
                    error = "WalletNotFound",
                    message = debitResult.ErrorMessage
                }),
                StatusCodes.Status401Unauthorized => StatusCode(StatusCodes.Status502BadGateway, new
                {
                    statusCode = StatusCodes.Status502BadGateway,
                    error = "InterServiceAuthFailure",
                    message = "Downstream WalletService rejected internal credentials."
                }),
                StatusCodes.Status400BadRequest => BadRequest(new
                {
                    statusCode = StatusCodes.Status400BadRequest,
                    error = "ValidationError",
                    message = debitResult.ErrorMessage
                }),
                _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    statusCode = StatusCodes.Status503ServiceUnavailable,
                    error = "ServiceUnavailable",
                    message = debitResult.ErrorMessage ?? "WalletService is currently unavailable. Please try again later."
                })
            };
        }

        var defaultCustomerId = Guid.Parse("c1000000-0000-0000-0000-000000000001");
        var fee = Math.Round(request.Amount * 0.015m, 2);

        var transaction = new Transaction
        {
            CustomerId = defaultCustomerId,
            Amount = request.Amount,
            Fee = fee,
            Currency = "NGN",
            Status = TransactionStatus.Completed,
            ReferenceNumber = referenceNumber,
            PaymentMethod = "Wallet",
            Description = $"{request.ItemDescription} (Wallet Debit: {debitResult.DebitId})",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Checkout completed successfully. Order {Ref}, Amount: ₦{Amount:N2} NGN, Remaining Balance: ₦{Bal:N2} NGN",
            referenceNumber, request.Amount, debitResult.NewBalance);

        return Ok(new CheckoutResponseDto
        {
            OrderId = transaction.Id,
            Status = "Completed",
            ReferenceNumber = referenceNumber,
            CustomerAccountId = request.CustomerAccountId,
            ItemDescription = request.ItemDescription,
            AmountPaid = request.Amount,
            Currency = "NGN",
            RemainingWalletBalance = debitResult.NewBalance,
            DebitTransactionId = debitResult.DebitId,
            ProcessedAt = transaction.CompletedAt ?? DateTime.UtcNow,
            Message = $"Checkout successful! ₦{request.Amount:N2} NGN was debited. Remaining wallet balance: ₦{debitResult.NewBalance:N2} NGN."
        });
    }

    [HttpGet("test-accounts")]
    public async Task<IActionResult> GetTestAccounts(CancellationToken ct)
    {
        var accounts = await _walletClient.GetDemoAccountsAsync(ct);
        return Ok(accounts);
    }
}

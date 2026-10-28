using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using XpressPay.Application.Features.Checkouts;

namespace XpressPay.Infrastructure.Clients;

public class WalletServiceClient : IWalletServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WalletServiceClient> _logger;

    public WalletServiceClient(HttpClient httpClient, ILogger<WalletServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<WalletDebitResult> DebitWalletAsync(DebitWalletCommand command, CancellationToken ct = default)
    {
        _logger.LogInformation("Sending REST debit request to WalletService for Account {AccountId}, Amount: ₦{Amount:N2} NGN, Ref: {Ref}",
            command.AccountId, command.Amount, command.ReferenceNumber);

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/wallets/debit", command, ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                return new WalletDebitResult
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    DebitId = content.GetProperty("debitId").GetGuid(),
                    AccountId = content.GetProperty("accountId").GetGuid(),
                    PreviousBalance = content.GetProperty("previousBalance").GetDecimal(),
                    DebitedAmount = content.GetProperty("debitedAmount").GetDecimal(),
                    NewBalance = content.GetProperty("newBalance").GetDecimal(),
                    Currency = content.GetProperty("currency").GetString() ?? "NGN",
                    ReferenceNumber = content.GetProperty("referenceNumber").GetString() ?? command.ReferenceNumber,
                    ProcessedAt = content.GetProperty("processedAt").GetDateTime()
                };
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            string errorMessage;
            try
            {
                using var doc = JsonDocument.Parse(errorBody);
                errorMessage = doc.RootElement.TryGetProperty("message", out var msgProp) 
                    ? msgProp.GetString() ?? errorBody 
                    : errorBody;
            }
            catch
            {
                errorMessage = errorBody;
            }

            _logger.LogWarning("WalletService returned non-success code {StatusCode}: {Error}",
                (int)response.StatusCode, errorMessage);

            return new WalletDebitResult
            {
                Success = false,
                StatusCode = (int)response.StatusCode,
                AccountId = command.AccountId,
                ErrorMessage = errorMessage
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP connection failure while communicating with WalletService at {BaseUrl}",
                _httpClient.BaseAddress);

            return new WalletDebitResult
            {
                Success = false,
                StatusCode = (int)HttpStatusCode.ServiceUnavailable,
                AccountId = command.AccountId,
                ErrorMessage = "Wallet service is currently unreachable. The checkout could not be processed."
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout while communicating with WalletService");

            return new WalletDebitResult
            {
                Success = false,
                StatusCode = (int)HttpStatusCode.GatewayTimeout,
                AccountId = command.AccountId,
                ErrorMessage = "Wallet service timed out while processing transaction."
            };
        }
    }

    public async Task<WalletAccountSummaryDto?> GetWalletAsync(Guid accountId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/wallets/{accountId}", ct);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<WalletAccountSummaryDto>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch wallet {AccountId} from WalletService", accountId);
            return null;
        }
    }

    public async Task<IReadOnlyList<WalletAccountSummaryDto>> GetDemoAccountsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/wallets/demo-accounts", ct);
            if (!response.IsSuccessStatusCode) return Array.Empty<WalletAccountSummaryDto>();

            var accounts = await response.Content.ReadFromJsonAsync<List<WalletAccountSummaryDto>>(cancellationToken: ct);
            return accounts ?? (IReadOnlyList<WalletAccountSummaryDto>)Array.Empty<WalletAccountSummaryDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch demo accounts from WalletService");
            return Array.Empty<WalletAccountSummaryDto>();
        }
    }
}

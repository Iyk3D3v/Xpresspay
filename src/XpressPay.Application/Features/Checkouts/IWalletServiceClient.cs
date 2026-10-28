namespace XpressPay.Application.Features.Checkouts;

public interface IWalletServiceClient
{
    Task<WalletDebitResult> DebitWalletAsync(DebitWalletCommand command, CancellationToken ct = default);

    Task<WalletAccountSummaryDto?> GetWalletAsync(Guid accountId, CancellationToken ct = default);

    Task<IReadOnlyList<WalletAccountSummaryDto>> GetDemoAccountsAsync(CancellationToken ct = default);
}

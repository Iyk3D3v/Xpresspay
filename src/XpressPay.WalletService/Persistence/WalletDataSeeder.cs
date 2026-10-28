using Microsoft.EntityFrameworkCore;
using XpressPay.WalletService.Domain;

namespace XpressPay.WalletService.Persistence;

public static class WalletDataSeeder
{
    public static readonly Guid AliceWalletId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid BobWalletId = Guid.Parse("b2000000-0000-0000-0000-000000000002");
    public static readonly Guid CharlieWalletId = Guid.Parse("c3000000-0000-0000-0000-000000000003");

    public static async Task SeedWalletDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WalletDbContext>();

        await context.Database.EnsureCreatedAsync();

        if (!await context.Wallets.AnyAsync())
        {
            var wallets = new List<Wallet>
            {
                new()
                {
                    Id = AliceWalletId,
                    AccountHolder = "Alice Tester",
                    Email = "alice@xpresspay.com",
                    Balance = 500000.00m,
                    Currency = "NGN",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-1),
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = BobWalletId,
                    AccountHolder = "Bob Budget",
                    Email = "bob@xpresspay.com",
                    Balance = 15000.00m,
                    Currency = "NGN",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-1),
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = CharlieWalletId,
                    AccountHolder = "Charlie Empty",
                    Email = "charlie@xpresspay.com",
                    Balance = 0.00m,
                    Currency = "NGN",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-1),
                    UpdatedAt = DateTime.UtcNow
                }
            };

            await context.Wallets.AddRangeAsync(wallets);
            await context.SaveChangesAsync();
        }
    }
}

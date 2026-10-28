using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XpressPay.Domain.Entities;
using XpressPay.Domain.Enums;

namespace XpressPay.Infrastructure.Persistence;

public static class OptimizationDataSeeder
{
    public static readonly Guid KnownCustomerId1 = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    public static readonly Guid KnownCustomerId2 = Guid.Parse("c2000000-0000-0000-0000-000000000002");
    public const string KnownReferenceNumber = "TXN-REF-100001";

    public static async Task SeedOptimizationDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("OptimizationDataSeeder");

        if (await context.Customers.AnyAsync())
        {
            logger.LogInformation("Customers and transactions already seeded. Skipping.");
            return;
        }

        logger.LogInformation("Seeding high-volume realistic dataset for query optimization testing...");

        var customers = new List<Customer>
        {
            new()
            {
                Id = KnownCustomerId1,
                Name = "Acme Global Logistics",
                Email = "billing@acmeglobal.com",
                PhoneNumber = "+1-555-0101",
                CreatedAt = DateTime.UtcNow.AddDays(-90)
            },
            new()
            {
                Id = KnownCustomerId2,
                Name = "Apex Fintech Solutions",
                Email = "finance@apexfintech.com",
                PhoneNumber = "+1-555-0102",
                CreatedAt = DateTime.UtcNow.AddDays(-80)
            }
        };

        for (int i = 3; i <= 10; i++)
        {
            customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                Name = $"Merchant Enterprise {i}",
                Email = $"merchant{i}@xpresspay-demo.com",
                PhoneNumber = $"+1-555-01{i:D2}",
                CreatedAt = DateTime.UtcNow.AddDays(-70 + i)
            });
        }

        await context.Customers.AddRangeAsync(customers);
        await context.SaveChangesAsync();

        var transactions = new List<Transaction>(3200);
        var random = new Random(42);
        var now = DateTime.UtcNow;
        var currencies = new[] { "USD", "EUR", "GBP" };
        var paymentMethods = new[] { "Card", "BankTransfer", "MobileMoney", "Crypto" };

        transactions.Add(new Transaction
        {
            Id = Guid.Parse("e1000000-0000-0000-0000-000000000001"),
            CustomerId = KnownCustomerId1,
            ReferenceNumber = KnownReferenceNumber,
            Amount = 1450.50m,
            Fee = 14.50m,
            Currency = "USD",
            Status = TransactionStatus.Completed,
            PaymentMethod = "Card",
            Description = "Corporate Subscription Payment",
            CreatedAt = now.AddDays(-10),
            CompletedAt = now.AddDays(-10).AddSeconds(15)
        });

        for (int i = 1; i <= 15; i++)
        {
            transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                CustomerId = KnownCustomerId1,
                ReferenceNumber = $"TXN-STALE-PENDING-{i:D4}",
                Amount = 100m + (i * 10),
                Fee = 2.5m,
                Currency = "USD",
                Status = TransactionStatus.Pending,
                PaymentMethod = "BankTransfer",
                Description = "Awaiting interbank settlement",
                CreatedAt = now.AddDays(-5).AddHours(i),
                CompletedAt = null
            });
        }

        for (int i = 1; i <= 5; i++)
        {
            transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                CustomerId = KnownCustomerId1,
                ReferenceNumber = $"TXN-FRESH-PENDING-{i:D4}",
                Amount = 50m + (i * 5),
                Fee = 1.25m,
                Currency = "USD",
                Status = TransactionStatus.Pending,
                PaymentMethod = "MobileMoney",
                Description = "Checkout in progress",
                CreatedAt = now.AddMinutes(-30 + (i * 2)),
                CompletedAt = null
            });
        }

        for (int i = 1; i <= 600; i++)
        {
            var daysAgo = (double)i / 10.0;
            var createdDate = now.AddDays(-daysAgo).AddMinutes(i % 60);
            var status = (i % 10 == 0) ? TransactionStatus.Failed :
                         (i % 25 == 0) ? TransactionStatus.Refunded : TransactionStatus.Completed;

            transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                CustomerId = KnownCustomerId1,
                ReferenceNumber = $"TXN-C1-{i:D5}",
                Amount = Math.Round((decimal)(random.NextDouble() * 500 + 10), 2),
                Fee = Math.Round((decimal)(random.NextDouble() * 5 + 0.5), 2),
                Currency = currencies[i % currencies.Length],
                Status = status,
                PaymentMethod = paymentMethods[i % paymentMethods.Length],
                Description = $"Merchant payment batch #{i}",
                CreatedAt = createdDate,
                CompletedAt = status == TransactionStatus.Completed ? createdDate.AddSeconds(20) : null
            });
        }

        for (int i = 1; i <= 2400; i++)
        {
            var customer = customers[random.Next(customers.Count)];
            var daysAgo = random.NextDouble() * 60;
            var createdDate = now.AddDays(-daysAgo);
            var statusRoll = random.Next(100);
            var status = statusRoll < 70 ? TransactionStatus.Completed :
                         statusRoll < 85 ? TransactionStatus.Pending :
                         statusRoll < 95 ? TransactionStatus.Failed : TransactionStatus.Refunded;

            transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                ReferenceNumber = $"TXN-BULK-{i:D6}",
                Amount = Math.Round((decimal)(random.NextDouble() * 1000 + 5), 2),
                Fee = Math.Round((decimal)(random.NextDouble() * 10 + 0.25), 2),
                Currency = currencies[random.Next(currencies.Length)],
                Status = status,
                PaymentMethod = paymentMethods[random.Next(paymentMethods.Length)],
                Description = $"Payment #{i} - {customer.Name}",
                CreatedAt = createdDate,
                CompletedAt = status == TransactionStatus.Completed ? createdDate.AddSeconds(15) : null
            });
        }

        await context.Transactions.AddRangeAsync(transactions);
        await context.SaveChangesAsync();

        logger.LogInformation("Successfully seeded {CustomerCount} customers and {TransactionCount} transactions.",
            customers.Count, transactions.Count);
    }
}

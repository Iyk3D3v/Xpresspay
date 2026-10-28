using Microsoft.EntityFrameworkCore;
using XpressPay.WalletService.Domain;

namespace XpressPay.WalletService.Persistence;

public class WalletDbContext : DbContext
{
    public WalletDbContext(DbContextOptions<WalletDbContext> options) : base(options)
    {
    }

    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.AccountHolder).IsRequired().HasMaxLength(150);
            entity.Property(w => w.Email).IsRequired().HasMaxLength(150);
            entity.Property(w => w.Currency).IsRequired().HasMaxLength(3);
            entity.Property(w => w.Balance).HasPrecision(18, 2);

            entity.HasMany(w => w.Transactions)
                  .WithOne(t => t.Wallet)
                  .HasForeignKey(t => t.WalletId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Amount).HasPrecision(18, 2);
            entity.Property(t => t.BalanceBefore).HasPrecision(18, 2);
            entity.Property(t => t.BalanceAfter).HasPrecision(18, 2);
            entity.Property(t => t.ReferenceNumber).IsRequired().HasMaxLength(100);
            entity.Property(t => t.Type).IsRequired().HasMaxLength(20);
        });
    }
}

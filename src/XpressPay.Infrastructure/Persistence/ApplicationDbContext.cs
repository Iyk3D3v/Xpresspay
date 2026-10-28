using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using XpressPay.Domain.Entities;
using XpressPay.Infrastructure.Identity;

namespace XpressPay.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(150);
            entity.Property(c => c.Email).IsRequired().HasMaxLength(150);
            entity.HasIndex(c => c.Email);

            entity.HasMany(c => c.Transactions)
                  .WithOne(t => t.Customer)
                  .HasForeignKey(t => t.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Transaction>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.ReferenceNumber).IsRequired().HasMaxLength(50);
            entity.Property(t => t.Currency).IsRequired().HasMaxLength(10);
            entity.Property(t => t.Amount).HasPrecision(18, 2);
            entity.Property(t => t.Fee).HasPrecision(18, 2);

            entity.HasIndex(t => t.ReferenceNumber)
                  .IsUnique();

            entity.HasIndex(t => new { t.CustomerId, t.CreatedAt });

            entity.HasIndex(t => new { t.Status, t.CreatedAt });

            entity.HasIndex(t => t.CreatedAt);
        });
    }
}

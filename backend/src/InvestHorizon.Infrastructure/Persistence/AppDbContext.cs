using InvestHorizon.Domain.Entities;
using InvestHorizon.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InvestHorizon.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<SaleAllocation> SaleAllocations => Set<SaleAllocation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Portfolio>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.BaseCurrency).HasMaxLength(3).IsRequired();
            e.HasMany(p => p.Transactions).WithOne(t => t.Portfolio).HasForeignKey(t => t.PortfolioId);
        });

        builder.Entity<Instrument>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => i.Isin).IsUnique();
            e.Property(i => i.Isin).HasMaxLength(12).IsRequired();
            e.Property(i => i.Name).HasMaxLength(300).IsRequired();
            e.Property(i => i.Currency).HasMaxLength(3).IsRequired();
            e.Property(i => i.Ticker).HasMaxLength(20);
            e.Property(i => i.Type).HasConversion<string>();
        });

        builder.Entity<Transaction>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Broker).HasConversion<string>();
            e.Property(t => t.Side).HasConversion<string>();
            e.Property(t => t.Currency).HasMaxLength(3).IsRequired();
            e.Property(t => t.UnitPrice).HasPrecision(18, 8);
            e.Property(t => t.Quantity).HasPrecision(18, 8);
            e.Property(t => t.FxRate).HasPrecision(18, 8);
            e.Property(t => t.AmountNative).HasPrecision(18, 4);
            e.Property(t => t.AmountEur).HasPrecision(18, 4);
            e.Property(t => t.BrokerFee).HasPrecision(18, 4);
            e.Property(t => t.TobAmount).HasPrecision(18, 4);
            e.Property(t => t.TotalCost).HasPrecision(18, 4);
            e.Property(t => t.NetProceeds).HasPrecision(18, 4);
            e.Property(t => t.RemainingQuantity).HasPrecision(18, 8);
            e.Property(t => t.CustodyFee).HasPrecision(18, 4);
            e.Property(t => t.ManualBrokerFee).HasPrecision(18, 4);

            e.HasOne(t => t.Instrument).WithMany(i => i.Transactions).HasForeignKey(t => t.InstrumentId);
            e.HasMany(t => t.SaleAllocationsAsBuy).WithOne(a => a.BuyTransaction).HasForeignKey(a => a.BuyTransactionId);
            e.HasMany(t => t.SaleAllocationsAsSell).WithOne(a => a.SellTransaction).HasForeignKey(a => a.SellTransactionId);
        });

        builder.Entity<SaleAllocation>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Quantity).HasPrecision(18, 8);
            e.Property(a => a.RealizedGainEur).HasPrecision(18, 4);
        });
    }
}

public class AppUser : IdentityUser
{
    public ICollection<Portfolio> Portfolios { get; set; } = new List<Portfolio>();
}

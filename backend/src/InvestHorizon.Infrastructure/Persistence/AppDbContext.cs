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
    public DbSet<InstrumentPrice> InstrumentPrices => Set<InstrumentPrice>();
    public DbSet<InstrumentPriceHistory> InstrumentPriceHistory => Set<InstrumentPriceHistory>();
    public DbSet<FxRateHistory> FxRateHistory => Set<FxRateHistory>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

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
            e.Property(i => i.PriceSymbol).HasMaxLength(30);
            e.Property(i => i.Type).HasConversion<string>();
            e.HasOne(i => i.LatestPrice).WithOne(p => p.Instrument).HasForeignKey<InstrumentPrice>(p => p.InstrumentId);
        });

        builder.Entity<InstrumentPrice>(e =>
        {
            e.HasKey(p => p.InstrumentId);
            e.Property(p => p.PriceNative).HasPrecision(18, 8);
            e.Property(p => p.Currency).HasMaxLength(3).IsRequired();
            e.Property(p => p.Source).HasMaxLength(40).IsRequired();
        });

        builder.Entity<InstrumentPriceHistory>(e =>
        {
            e.HasKey(p => new { p.InstrumentId, p.Date });
            e.Property(p => p.CloseNative).HasPrecision(18, 8);
            e.Property(p => p.Currency).HasMaxLength(3).IsRequired();
            e.HasOne(p => p.Instrument).WithMany().HasForeignKey(p => p.InstrumentId);
        });

        builder.Entity<FxRateHistory>(e =>
        {
            e.HasKey(r => new { r.Currency, r.Date });
            e.Property(r => r.Currency).HasMaxLength(3).IsRequired();
            e.Property(r => r.RatePerEur).HasPrecision(18, 8);
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

        builder.Entity<Recommendation>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.UserId).IsRequired();
            e.Property(r => r.Source).HasMaxLength(200).IsRequired();
            e.Property(r => r.Rating).HasConversion<string>();
            e.Property(r => r.Comment).HasMaxLength(4000);
            e.HasIndex(r => new { r.UserId, r.InstrumentId });
            e.HasOne(r => r.Instrument).WithMany(i => i.Recommendations).HasForeignKey(r => r.InstrumentId);
        });
    }
}

public class AppUser : IdentityUser
{
    public ICollection<Portfolio> Portfolios { get; set; } = new List<Portfolio>();
}

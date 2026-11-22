using Microsoft.EntityFrameworkCore;
using TradingJournal.Core.Entities;

namespace TradingJournal.Data;

// Veritabanının C# tarafındaki karşılığı. Her DbSet bir tabloya karşılık gelir.
public class TradingJournalDbContext : DbContext
{
    public TradingJournalDbContext(DbContextOptions<TradingJournalDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountRule> AccountRules => Set<AccountRule>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<Strategy> Strategies => Set<Strategy>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<TradeScreenshot> TradeScreenshots => Set<TradeScreenshot>();
    public DbSet<StrategyRule> StrategyRules => Set<StrategyRule>();
    public DbSet<TradeRuleCheck> TradeRuleChecks => Set<TradeRuleCheck>();
    public DbSet<DailyNote> DailyNotes => Set<DailyNote>();

    // ForexTrade ve CryptoTrade ayrı tablo değil; Trades tablosundaki "TradeType"
    // sütunuyla ayırt edilir (TPH: Table-Per-Hierarchy). Ayarlar Configurations klasöründe.
    public DbSet<ForexTrade> ForexTrades => Set<ForexTrade>();
    public DbSet<CryptoTrade> CryptoTrades => Set<CryptoTrade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configurations klasöründeki tüm IEntityTypeConfiguration sınıflarını otomatik uygular.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradingJournalDbContext).Assembly);
    }
}

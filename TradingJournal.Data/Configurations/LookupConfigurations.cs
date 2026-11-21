using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;

namespace TradingJournal.Data.Configurations;

public class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.ToTable("Instruments");
        builder.Property(i => i.Symbol).IsRequired().HasMaxLength(20);
        builder.HasIndex(i => i.Symbol).IsUnique();

        // Uygulama ilk açıldığında hazır gelen enstrümanlar (seed data).
        // Pip değerleri 1 standart lot için yaklaşık USD karşılığıdır.
        builder.HasData(
            new Instrument { Id = 1, Symbol = "EURUSD", Type = InstrumentType.Forex, PipSize = 0.0001m, PipValuePerLot = 10m },
            new Instrument { Id = 2, Symbol = "GBPUSD", Type = InstrumentType.Forex, PipSize = 0.0001m, PipValuePerLot = 10m },
            new Instrument { Id = 3, Symbol = "USDJPY", Type = InstrumentType.Forex, PipSize = 0.01m, PipValuePerLot = 6.5m },
            new Instrument { Id = 4, Symbol = "XAUUSD", Type = InstrumentType.Forex, PipSize = 0.1m, PipValuePerLot = 10m },
            new Instrument { Id = 5, Symbol = "BTCUSDT", Type = InstrumentType.Crypto, PipSize = 0.01m, PipValuePerLot = 1m },
            new Instrument { Id = 6, Symbol = "ETHUSDT", Type = InstrumentType.Crypto, PipSize = 0.01m, PipValuePerLot = 1m });
    }
}

public class StrategyConfiguration : IEntityTypeConfiguration<Strategy>
{
    public void Configure(EntityTypeBuilder<Strategy> builder)
    {
        builder.ToTable("Strategies");
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(s => s.Name).IsUnique();
        builder.Property(s => s.Description).HasMaxLength(1000);
    }
}

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.Property(t => t.Name).IsRequired().HasMaxLength(40);
        builder.HasIndex(t => t.Name).IsUnique();
        builder.Property(t => t.ColorHex).HasMaxLength(7);

        builder.HasData(
            new Tag { Id = 1, Name = "A+ Setup", ColorHex = "#2E7D32" },
            new Tag { Id = 2, Name = "FOMO", ColorHex = "#C62828" },
            new Tag { Id = 3, Name = "Revenge", ColorHex = "#AD1457" },
            new Tag { Id = 4, Name = "Haber Günü", ColorHex = "#F9A825" },
            new Tag { Id = 5, Name = "Erken Çıkış", ColorHex = "#6A1B9A" });
    }
}

public class TradeScreenshotConfiguration : IEntityTypeConfiguration<TradeScreenshot>
{
    public void Configure(EntityTypeBuilder<TradeScreenshot> builder)
    {
        builder.ToTable("TradeScreenshots");
        builder.Property(s => s.FilePath).IsRequired().HasMaxLength(500);
        builder.Property(s => s.Caption).HasMaxLength(200);

        builder.HasOne(s => s.Trade)
            .WithMany(t => t.Screenshots)
            .HasForeignKey(s => s.TradeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

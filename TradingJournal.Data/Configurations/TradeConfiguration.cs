using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingJournal.Core.Entities;

namespace TradingJournal.Data.Configurations;

public class TradeConfiguration : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        builder.ToTable("Trades");

        // Kalıtım veritabanına böyle yansır: tek tablo + tür sütunu.
        builder.HasDiscriminator<string>("TradeType")
            .HasValue<ForexTrade>("Forex")
            .HasValue<CryptoTrade>("Crypto");

        builder.Property(t => t.Notes).HasMaxLength(4000);
        builder.Property(t => t.ExternalId).HasMaxLength(64);

        // Hesap silinirse o hesabın işlemleri de silinir.
        builder.HasOne(t => t.Account)
            .WithMany(a => a.Trades)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // İşlemi olan bir enstrüman silinemez.
        builder.HasOne(t => t.Instrument)
            .WithMany(i => i.Trades)
            .HasForeignKey(t => t.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Strateji silinirse işlemler kalır, sadece stratejisi boşalır.
        builder.HasOne(t => t.Strategy)
            .WithMany(s => s.Trades)
            .HasForeignKey(t => t.StrategyId)
            .OnDelete(DeleteBehavior.SetNull);

        // Çoka-çok: EF Core araya otomatik "TradeTags" ara tablosunu oluşturur.
        builder.HasMany(t => t.Tags)
            .WithMany(tag => tag.Trades)
            .UsingEntity(j => j.ToTable("TradeTags"));

        builder.HasIndex(t => t.OpenTime);

        // SQLite'ta NULL değerler benzersizlik kuralına takılmaz: elle girilen işlemler sorunsuz.
        builder.HasIndex(t => t.ExternalId).IsUnique();
    }
}

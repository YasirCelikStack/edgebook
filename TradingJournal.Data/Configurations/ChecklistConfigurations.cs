using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingJournal.Core.Entities;

namespace TradingJournal.Data.Configurations;

public class StrategyRuleConfiguration : IEntityTypeConfiguration<StrategyRule>
{
    public void Configure(EntityTypeBuilder<StrategyRule> builder)
    {
        builder.ToTable("StrategyRules");
        builder.Property(r => r.Text).IsRequired().HasMaxLength(200);

        // Strateji silinirse kuralları da silinir.
        builder.HasOne(r => r.Strategy)
            .WithMany(s => s.Rules)
            .HasForeignKey(r => r.StrategyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TradeRuleCheckConfiguration : IEntityTypeConfiguration<TradeRuleCheck>
{
    public void Configure(EntityTypeBuilder<TradeRuleCheck> builder)
    {
        builder.ToTable("TradeRuleChecks");

        // Bileşik anahtar: bir işlemde her kural en fazla bir kez cevaplanır.
        builder.HasKey(c => new { c.TradeId, c.StrategyRuleId });

        builder.HasOne(c => c.Trade)
            .WithMany(t => t.RuleChecks)
            .HasForeignKey(c => c.TradeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Rule)
            .WithMany(r => r.Checks)
            .HasForeignKey(c => c.StrategyRuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DailyNoteConfiguration : IEntityTypeConfiguration<DailyNote>
{
    public void Configure(EntityTypeBuilder<DailyNote> builder)
    {
        builder.ToTable("DailyNotes");
        builder.Property(n => n.Plan).HasMaxLength(4000);
        builder.Property(n => n.Review).HasMaxLength(4000);

        // Her gün için tek not.
        builder.HasIndex(n => n.Date).IsUnique();
    }
}

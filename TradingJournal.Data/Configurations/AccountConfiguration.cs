using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingJournal.Core.Entities;

namespace TradingJournal.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.Property(a => a.Name).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Broker).HasMaxLength(100);
        builder.Property(a => a.Currency).IsRequired().HasMaxLength(3);
    }
}

public class AccountRuleConfiguration : IEntityTypeConfiguration<AccountRule>
{
    public void Configure(EntityTypeBuilder<AccountRule> builder)
    {
        builder.ToTable("AccountRules");

        builder.HasOne(r => r.Account)
            .WithMany(a => a.Rules)
            .HasForeignKey(r => r.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bir hesapta aynı türden iki kural olmasın (örn. iki ayrı günlük kayıp kuralı).
        builder.HasIndex(r => new { r.AccountId, r.RuleType }).IsUnique();
    }
}

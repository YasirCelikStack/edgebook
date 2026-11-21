using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;

namespace TradingJournal.Services.Risk;

public class RiskService
{
    // Factory: veritabanındaki AccountRule kaydından doğru kural nesnesini üretir.
    // Yeni bir kural türü eklemek için sadece yeni bir sınıf yazıp buraya bir satır eklemek yeterli.
    public static IRiskRule CreateRule(AccountRule rule) => rule.RuleType switch
    {
        RiskRuleType.DailyLoss => new DailyLossRule(rule.LimitPercent),
        RiskRuleType.MaxDrawdown => new MaxDrawdownRule(rule.LimitPercent),
        _ => throw new NotSupportedException($"Bilinmeyen kural türü: {rule.RuleType}")
    };

    // Hesabın aktif tüm kurallarını tek döngüde kontrol eder.
    // Döngü kuralın türünü bilmez; her biri kendi Check() uygulamasını çalıştırır.
    public List<RiskCheckResult> CheckAccount(Account account, IReadOnlyCollection<Trade> trades, DateTime asOf)
    {
        var results = new List<RiskCheckResult>();

        foreach (AccountRule ruleEntity in account.Rules.Where(r => r.IsActive))
        {
            IRiskRule rule = CreateRule(ruleEntity);
            results.Add(rule.Check(account, trades, asOf));
        }

        return results;
    }
}

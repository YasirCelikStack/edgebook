using TradingJournal.Core.Entities;
using TradingJournal.Core.Exceptions;

namespace TradingJournal.Services.Risk;

// Ortak kısımları (isim, yüzde limit, limitin tutara çevrilmesi) tek yerde toplayan soyut sınıf.
// Alt sınıflar sadece "kaybı nasıl ölçerim?" sorusunu cevaplar: CalculateCurrentLoss().
public abstract class RiskRuleBase : IRiskRule
{
    protected RiskRuleBase(decimal limitPercent)
    {
        if (limitPercent <= 0 || limitPercent > 100)
            throw new DomainException("Kural limiti 0 ile 100 arasında olmalıdır.");
        LimitPercent = limitPercent;
    }

    public decimal LimitPercent { get; }
    public abstract string Name { get; }

    // Template Method: kontrolün iskeleti burada sabit, değişen tek adım alt sınıfta.
    public RiskCheckResult Check(Account account, IReadOnlyCollection<Trade> trades, DateTime asOf)
    {
        decimal limitAmount = account.InitialBalance * LimitPercent / 100m;
        decimal currentLoss = CalculateCurrentLoss(account, trades, asOf);
        return new RiskCheckResult(Name, limitAmount, Math.Max(0, currentLoss));
    }

    protected abstract decimal CalculateCurrentLoss(Account account, IReadOnlyCollection<Trade> trades, DateTime asOf);

    protected static IEnumerable<Trade> ClosedUntil(IEnumerable<Trade> trades, DateTime asOf)
        => trades.Where(t => t.IsClosed && t.CloseTime <= asOf);
}

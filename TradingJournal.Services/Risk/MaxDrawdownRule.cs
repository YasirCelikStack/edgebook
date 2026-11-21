using TradingJournal.Core.Entities;

namespace TradingJournal.Services.Risk;

// Toplam drawdown kuralı: bakiye, başlangıç bakiyesinin altına limitten fazla düşmemeli.
// Çoğu prop-firm'deki gibi "statik" drawdown: referans hep başlangıç bakiyesi.
// Örn. 100.000$ hesap, %10 limit => bakiye hiçbir zaman 90.000$'ın altına inmemeli.
public class MaxDrawdownRule : RiskRuleBase
{
    public MaxDrawdownRule(decimal limitPercent) : base(limitPercent)
    {
    }

    public override string Name => $"Maksimum drawdown (%{LimitPercent})";

    protected override decimal CalculateCurrentLoss(Account account, IReadOnlyCollection<Trade> trades, DateTime asOf)
    {
        // İşlemleri sırayla oynatıp bakiyenin gördüğü en düşük noktayı buluruz.
        decimal balance = account.InitialBalance;
        decimal lowest = balance;

        foreach (Trade trade in ClosedUntil(trades, asOf).OrderBy(t => t.CloseTime))
        {
            balance += trade.NetPnL!.Value;
            lowest = Math.Min(lowest, balance);
        }

        return account.InitialBalance - lowest;
    }
}

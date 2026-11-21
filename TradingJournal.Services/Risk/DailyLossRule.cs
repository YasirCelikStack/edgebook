using TradingJournal.Core.Entities;

namespace TradingJournal.Services.Risk;

// Günlük kayıp kuralı: verilen günde kapanan işlemlerin toplam zararı limiti aşmamalı.
// Örn. 100.000$ hesap, %5 limit => bir günde en fazla 5.000$ kaybedilebilir.
public class DailyLossRule : RiskRuleBase
{
    public DailyLossRule(decimal limitPercent) : base(limitPercent)
    {
    }

    public override string Name => $"Günlük kayıp (%{LimitPercent})";

    protected override decimal CalculateCurrentLoss(Account account, IReadOnlyCollection<Trade> trades, DateTime asOf)
    {
        decimal todaysPnL = ClosedUntil(trades, asOf)
            .Where(t => t.CloseTime!.Value.Date == asOf.Date)
            .Sum(t => t.NetPnL!.Value);

        // Gün kârdaysa kayıp sıfırdır.
        return -todaysPnL;
    }
}

using TradingJournal.Core.Entities;

namespace TradingJournal.Services.Statistics;

// Not: Hesaplar veritabanında değil bellekte (LINQ to Objects) yapılır.
// SQLite, decimal sütunlarda SUM/ORDER BY gibi işlemleri doğru desteklemez.
public class StatisticsService : IStatisticsService
{
    public TradeStatistics Calculate(IEnumerable<Trade> trades)
    {
        // Açık işlemlerin sonucu henüz belli değil; sadece kapanmışları sayarız.
        List<Trade> closed = trades.Where(t => t.IsClosed).OrderBy(t => t.CloseTime).ToList();
        if (closed.Count == 0)
            return TradeStatistics.Empty;

        List<decimal> results = closed.Select(t => t.NetPnL!.Value).ToList();
        List<decimal> wins = results.Where(r => r > 0).ToList();
        List<decimal> losses = results.Where(r => r < 0).ToList();
        List<decimal> rValues = closed.Where(t => t.RMultiple.HasValue).Select(t => t.RMultiple!.Value).ToList();

        return new TradeStatistics
        {
            TotalTrades = closed.Count,
            Wins = wins.Count,
            Losses = losses.Count,
            NetProfit = results.Sum(),
            GrossProfit = wins.Sum(),
            GrossLoss = losses.Sum(),
            AverageWin = wins.Count == 0 ? 0 : Math.Round(wins.Average(), 2),
            AverageLoss = losses.Count == 0 ? 0 : Math.Round(losses.Average(), 2),
            AverageR = rValues.Count == 0 ? null : Math.Round(rValues.Average(), 2),
            LargestWin = wins.Count == 0 ? 0 : wins.Max(),
            LargestLoss = losses.Count == 0 ? 0 : losses.Min(),
            MaxDrawdown = CalculateMaxDrawdown(results)
        };
    }

    public List<EquityPoint> BuildEquityCurve(decimal initialBalance, IEnumerable<Trade> trades)
    {
        var curve = new List<EquityPoint>();
        decimal balance = initialBalance;

        foreach (Trade trade in trades.Where(t => t.IsClosed).OrderBy(t => t.CloseTime))
        {
            decimal pnl = trade.NetPnL!.Value;
            balance += pnl;
            curve.Add(new EquityPoint(trade.CloseTime!.Value, balance, pnl));
        }

        return curve;
    }

    public List<GroupStatistics<TKey>> GroupBy<TKey>(IEnumerable<Trade> trades, Func<Trade, TKey> keySelector)
        where TKey : notnull
    {
        return trades
            .Where(t => t.IsClosed)
            .GroupBy(keySelector)
            .Select(g => new GroupStatistics<TKey>(g.Key, Calculate(g)))
            .OrderByDescending(g => g.Stats.TotalTrades)
            .ToList();
    }

    // Sırayla gelen sonuçlarda, ulaşılan en yüksek noktadan sonraki en büyük düşüş.
    // Örn. +100, +50, -80, -40, +30 => zirve 150, dip 30 => drawdown 120
    private static decimal CalculateMaxDrawdown(IEnumerable<decimal> resultsInOrder)
    {
        decimal cumulative = 0, peak = 0, maxDrawdown = 0;

        foreach (decimal result in resultsInOrder)
        {
            cumulative += result;
            peak = Math.Max(peak, cumulative);
            maxDrawdown = Math.Max(maxDrawdown, peak - cumulative);
        }

        return maxDrawdown;
    }
}

using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.Services.Statistics;

namespace TradingJournal.Services.Analytics;

// Trader davranışı: ne kadar süre işlemde kalıyor, ne kadar risk alıyor, hedefi ne kadar büyük.
public record BehaviorStatistics(
    TimeSpan? AverageHoldTime,
    TimeSpan? AverageWinnerHoldTime,
    TimeSpan? AverageLoserHoldTime,
    decimal AverageRiskAmount,
    decimal? AverageRiskPercent,
    decimal? AveragePlannedRR,
    int MaxWinStreak,
    int MaxLossStreak,
    int CurrentStreak);    // + ise art arda kazanç, - ise art arda kayıp

// "Asya'da EURUSD Long" gibi bir kombinasyonun performansı.
public record EdgeResult(string Label, int Trades, decimal WinRate, decimal AverageR, decimal NetProfit);

// İşlemleri farklı açılardan analiz eden servis. Hesaplar bellekte (LINQ) yapılır.
public class TradeAnalyticsService
{
    private readonly IStatisticsService _statistics;

    // Bağımlılık dışarıdan verilebilir (test için farklı bir uygulama takılabilir).
    public TradeAnalyticsService(IStatisticsService? statistics = null)
    {
        _statistics = statistics ?? new StatisticsService();
    }

    public BehaviorStatistics CalculateBehavior(IReadOnlyCollection<Trade> trades)
    {
        List<Trade> closed = trades.Where(t => t.IsClosed && t.CloseTime.HasValue)
            .OrderBy(t => t.CloseTime).ToList();

        static TimeSpan? AverageDuration(IEnumerable<Trade> list)
        {
            var ticks = list.Select(t => (t.CloseTime!.Value - t.OpenTime).Ticks).Where(x => x >= 0).ToList();
            return ticks.Count == 0 ? null : TimeSpan.FromTicks((long)ticks.Average());
        }

        var risks = trades.Select(t => t.CalculateRiskAmount()).ToList();
        var riskPercents = trades
            .Where(t => t.Account is not null && t.Account.InitialBalance > 0)
            .Select(t => t.CalculateRiskAmount() / t.Account.InitialBalance * 100m)
            .ToList();
        var plannedRR = trades
            .Where(t => t.TakeProfit.HasValue && t.EntryPrice != t.StopLoss)
            .Select(t => Math.Abs(t.TakeProfit!.Value - t.EntryPrice) / Math.Abs(t.EntryPrice - t.StopLoss))
            .ToList();

        (int maxWin, int maxLoss, int current) = Streaks(closed);

        return new BehaviorStatistics(
            AverageDuration(closed),
            AverageDuration(closed.Where(t => t.NetPnL > 0)),
            AverageDuration(closed.Where(t => t.NetPnL < 0)),
            risks.Count == 0 ? 0 : Math.Round(risks.Average(), 2),
            riskPercents.Count == 0 ? null : Math.Round(riskPercents.Average(), 2),
            plannedRR.Count == 0 ? null : Math.Round(plannedRR.Average(), 2),
            maxWin, maxLoss, current);
    }

    private static (int MaxWin, int MaxLoss, int Current) Streaks(List<Trade> closedInOrder)
    {
        int win = 0, loss = 0, maxWin = 0, maxLoss = 0;
        foreach (Trade t in closedInOrder)
        {
            if (t.NetPnL > 0) { win++; loss = 0; }
            else if (t.NetPnL < 0) { loss++; win = 0; }
            maxWin = Math.Max(maxWin, win);
            maxLoss = Math.Max(maxLoss, loss);
        }
        return (maxWin, maxLoss, win > 0 ? win : -loss);
    }

    // Sembol × seans × yön kombinasyonlarını sıralar. Az işlemli kombinasyonlar yanıltıcı
    // olacağı için minTrades altındakiler elenir.
    public List<EdgeResult> FindEdges(IEnumerable<Trade> trades, int minTrades = 5)
    {
        return _statistics
            .GroupBy(trades, t => (
                Symbol: t.Instrument?.Symbol ?? "?",
                Session: SessionClassifier.GetSession(t.OpenTime),
                t.Direction))
            .Where(g => g.Stats.TotalTrades >= minTrades)
            .Select(g => new EdgeResult(
                $"{g.Key.Session.ToTurkish()} · {g.Key.Symbol} {(g.Key.Direction == TradeDirection.Long ? "Long" : "Short")}",
                g.Stats.TotalTrades,
                g.Stats.WinRate,
                g.Stats.AverageR ?? 0,
                g.Stats.NetProfit))
            .ToList();
    }
}

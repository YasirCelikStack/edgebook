using TradingJournal.Core.Entities;

namespace TradingJournal.Services.Statistics;

public interface IStatisticsService
{
    TradeStatistics Calculate(IEnumerable<Trade> trades);

    List<EquityPoint> BuildEquityCurve(decimal initialBalance, IEnumerable<Trade> trades);

    // Generic metot: işlemleri herhangi bir özelliğe göre gruplayıp her grubun istatistiğini çıkarır.
    // Örn. GroupBy(trades, t => t.OpenTime.DayOfWeek) veya GroupBy(trades, t => t.Instrument.Symbol)
    List<GroupStatistics<TKey>> GroupBy<TKey>(IEnumerable<Trade> trades, Func<Trade, TKey> keySelector)
        where TKey : notnull;
}

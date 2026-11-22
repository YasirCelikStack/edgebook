using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.Services.Statistics;

namespace TradingJournal.Services.Analytics;

// Tek bir kuralın etkisi: o kurala uyulduğunda ve uyulmadığında sonuçlar.
public record RuleImpact(string Strategy, string Rule, int Tracked, int Broken, decimal? AverageRWhenMet, decimal? AverageRWhenBroken)
{
    public decimal BrokenRate => Tracked == 0 ? 0 : Math.Round(100m * Broken / Tracked, 1);
}

// Kurala uyum özeti: uyulan / çiğnenen işlemlerin istatistikleri ve en çok çiğnenen kurallar.
public record ComplianceSummary(
    TradeStatistics Followed,
    TradeStatistics Broken,
    int NotTracked,
    List<RuleImpact> Rules)
{
    public int Tracked => Followed.TotalTrades + Broken.TotalTrades;

    // Kontrol listesi doldurulan işlemlerin yüzde kaçında tüm kurallara uyuldu.
    public decimal? FollowRate => Tracked == 0 ? null : Math.Round(100m * Followed.TotalTrades / Tracked, 1);
}

// Strateji kontrol listelerinden "disiplin" analizi çıkarır. Sadece kapanmış işlemler sayılır.
public class RuleComplianceService
{
    private readonly IStatisticsService _statistics;

    public RuleComplianceService(IStatisticsService? statistics = null)
    {
        _statistics = statistics ?? new StatisticsService();
    }

    public ComplianceSummary Calculate(IEnumerable<Trade> trades)
    {
        List<Trade> closed = trades.Where(t => t.IsClosed).ToList();

        List<RuleImpact> rules = closed
            .SelectMany(t => t.RuleChecks.Select(c => (Trade: t, Check: c)))
            .Where(x => x.Check.Rule is not null)
            .GroupBy(x => x.Check.StrategyRuleId)
            .Select(g =>
            {
                StrategyRule rule = g.First().Check.Rule;
                List<decimal> met = g.Where(x => x.Check.IsMet && x.Trade.RMultiple.HasValue).Select(x => x.Trade.RMultiple!.Value).ToList();
                List<decimal> broken = g.Where(x => !x.Check.IsMet && x.Trade.RMultiple.HasValue).Select(x => x.Trade.RMultiple!.Value).ToList();
                return new RuleImpact(
                    g.First().Trade.Strategy?.Name ?? "",
                    rule.Text,
                    g.Count(),
                    g.Count(x => !x.Check.IsMet),
                    met.Count == 0 ? null : Math.Round(met.Average(), 2),
                    broken.Count == 0 ? null : Math.Round(broken.Average(), 2));
            })
            .Where(r => r.Broken > 0)
            .OrderByDescending(r => r.Broken)
            .ThenBy(r => r.AverageRWhenBroken)
            .ToList();

        return new ComplianceSummary(
            _statistics.Calculate(closed.Where(t => t.Compliance == RuleCompliance.Followed)),
            _statistics.Calculate(closed.Where(t => t.Compliance == RuleCompliance.Broken)),
            closed.Count(t => t.Compliance == RuleCompliance.NotTracked),
            rules);
    }
}

public static class RuleComplianceText
{
    public static string ToTurkish(this RuleCompliance compliance) => compliance switch
    {
        RuleCompliance.Followed => "Kurallara uyuldu",
        RuleCompliance.Broken => "Kural çiğnendi",
        _ => "Kontrol listesi yok"
    };
}

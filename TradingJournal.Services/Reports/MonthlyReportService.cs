using System.Globalization;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.Core.Exceptions;
using TradingJournal.Services.Analytics;
using TradingJournal.Services.Statistics;

namespace TradingJournal.Services.Reports;

// Seçilen ay için rapor verisini hazırlar. Bir işlem, KAPANDIĞI aya sayılır
// (kâr/zarar o gün gerçekleşir); takvim ve equity curve de aynı kuralı kullanır.
public class MonthlyReportService
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private readonly IStatisticsService _statistics;
    private readonly RuleComplianceService _compliance;

    public MonthlyReportService(IStatisticsService? statistics = null)
    {
        _statistics = statistics ?? new StatisticsService();
        _compliance = new RuleComplianceService(_statistics);
    }

    public static string MonthName(DateTime month) => Turkish.DateTimeFormat.GetMonthName(month.Month);

    // İçinde kapanmış işlem olan aylar, en yeniden eskiye.
    public List<ReportMonth> GetAvailableMonths(IEnumerable<Trade> trades)
        => trades
            .Where(t => t.IsClosed && t.CloseTime.HasValue)
            .GroupBy(t => new DateTime(t.CloseTime!.Value.Year, t.CloseTime.Value.Month, 1))
            .Select(g => new ReportMonth(g.Key, g.Count()))
            .OrderByDescending(m => m.Month)
            .ToList();

    // accounts: raporun kapsadığı hesaplar (tek hesap ya da "tüm hesaplar").
    // trades: bu hesapların TÜM işlemleri (ay başı bakiyesini bulmak için önceki aylar da gerekir).
    // notes: ayın günlük notları (yoksa boş).
    public MonthlyReportData Build(DateTime month, IReadOnlyCollection<Account> accounts, IReadOnlyCollection<Trade> trades,
        IReadOnlyCollection<DailyNote>? notes = null)
    {
        if (accounts.Count == 0)
            throw new DomainException("Rapor için en az bir hesap gerekli.");

        DateTime start = new(month.Year, month.Month, 1);
        DateTime end = start.AddMonths(1);
        DateTime previousStart = start.AddMonths(-1);

        List<Trade> closed = trades.Where(t => t.IsClosed && t.CloseTime.HasValue).ToList();
        List<Trade> inMonth = closed.Where(t => t.CloseTime >= start && t.CloseTime < end).OrderBy(t => t.CloseTime).ToList();
        List<Trade> inPrevious = closed.Where(t => t.CloseTime >= previousStart && t.CloseTime < start).ToList();

        decimal initial = accounts.Sum(a => a.InitialBalance);
        decimal startBalance = initial + closed.Where(t => t.CloseTime < start).Sum(t => t.NetPnL!.Value);

        List<EquityPoint> equity = [new EquityPoint(start, startBalance, 0)];
        equity.AddRange(_statistics.BuildEquityCurve(startBalance, inMonth));

        TradeStatistics stats = _statistics.Calculate(inMonth);
        List<string> currencies = accounts.Select(a => a.Currency).Distinct().ToList();

        var data = new MonthlyReportData
        {
            Month = start,
            AccountLabel = accounts.Count == 1 ? accounts.First().Name : $"Tüm hesaplar ({accounts.Count})",
            Currency = currencies.Count == 1 ? currencies[0] : "",
            Stats = stats,
            PreviousStats = _statistics.Calculate(inPrevious),
            StartBalance = startBalance,
            EndBalance = startBalance + stats.NetProfit,
            OpenTradeCount = trades.Count(t => !t.IsClosed && t.OpenTime >= start && t.OpenTime < end),
            Equity = equity,
            Days = inMonth
                .GroupBy(t => t.CloseTime!.Value.Date)
                .Select(g => new DailyResult(g.Key, g.Sum(t => t.NetPnL!.Value), g.Count()))
                .OrderBy(d => d.Date)
                .ToList(),
            BySetup = Breakdown(inMonth, t => t.Strategy?.Name ?? "Setup yok"),
            BySymbol = Breakdown(inMonth, t => t.Instrument?.Symbol ?? "?"),
            BySession = Breakdown(inMonth, t => SessionClassifier.GetSession(t.OpenTime).ToTurkish()),
            // Pazartesi = 0 ... Pazar = 6 (DayOfWeek Pazar'dan başlar; haftayı Pazartesi'den sıralamak için kaydırılır).
            ByWeekday = Breakdown(inMonth, t => ((int)t.OpenTime.DayOfWeek + 6) % 7,
                i => Turkish.DateTimeFormat.GetDayName((DayOfWeek)((i + 1) % 7)), sortByKey: true),
            Insights = [],
            Trades = inMonth.Select(ToRow).ToList(),
            Compliance = _compliance.Calculate(inMonth),
            Notes = (notes ?? Array.Empty<DailyNote>()).Where(n => n.Date >= start && n.Date < end).OrderBy(n => n.Date).ToList()
        };

        data.Insights.AddRange(BuildInsights(data));
        return data;
    }

    // Generic kırılım: herhangi bir anahtara göre grupla, her grubun istatistiğini çıkar.
    private List<BreakdownRow> Breakdown<TKey>(IEnumerable<Trade> trades, Func<Trade, TKey> key,
        Func<TKey, string>? label = null, bool sortByKey = false) where TKey : notnull
    {
        IEnumerable<GroupStatistics<TKey>> groups = _statistics.GroupBy(trades, key);
        groups = sortByKey
            ? groups.OrderBy(g => g.Key)
            : groups.OrderByDescending(g => g.Stats.NetProfit);
        return groups
            .Select(g => new BreakdownRow(label is null ? g.Key.ToString()! : label(g.Key),
                g.Stats.TotalTrades, g.Stats.WinRate, g.Stats.NetProfit, g.Stats.AverageR))
            .ToList();
    }

    private static ReportTradeRow ToRow(Trade t) => new()
    {
        OpenTime = t.OpenTime,
        CloseTime = t.CloseTime,
        Symbol = t.Instrument?.Symbol ?? "?",
        Direction = t.Direction == TradeDirection.Long ? "Long" : "Short",
        Setup = t.Strategy?.Name ?? "",
        Session = SessionClassifier.GetSession(t.OpenTime).ToTurkish(),
        EntryPrice = t.EntryPrice,
        ExitPrice = t.ExitPrice,
        RMultiple = t.RMultiple is decimal r ? Math.Round(r, 2) : null,
        NetPnL = t.NetPnL,
        Tags = string.Join(", ", t.Tags.Select(x => x.Name).OrderBy(x => x)),
        Rating = t.Rating
    };

    // Raporun ilk sayfasındaki "Bu ayın özeti" maddeleri. Sadece verinin kendisinden çıkan,
    // yoruma yer bırakmayan gözlemler (yorumlu analiz için yapay zeka koçu var).
    private static IEnumerable<string> BuildInsights(MonthlyReportData d)
    {
        if (d.IsEmpty)
        {
            yield return "Bu ay kapanan işlem yok.";
            yield break;
        }

        TradeStatistics s = d.Stats, p = d.PreviousStats;
        if (p.TotalTrades > 0)
        {
            string direction = s.NetProfit >= p.NetProfit ? "daha iyi" : "daha zayıf";
            yield return $"Geçen aya göre {direction}: net {Money(s.NetProfit, d.Currency)} (geçen ay {Money(p.NetProfit, d.Currency)}), " +
                         $"win rate %{s.WinRate:0.#} (geçen ay %{p.WinRate:0.#}).";
        }

        if (d.Days.Count > 0)
        {
            DailyResult best = d.Days.MaxBy(x => x.NetPnL)!;
            DailyResult worst = d.Days.MinBy(x => x.NetPnL)!;
            int green = d.Days.Count(x => x.NetPnL > 0);
            yield return $"{d.Days.Count} işlem gününün {green} tanesi kârlı. En iyi gün {best.Date:dd.MM} ({Money(best.NetPnL, d.Currency)})" +
                         (worst.NetPnL < 0 ? $", en kötü gün {worst.Date:dd.MM} ({Money(worst.NetPnL, d.Currency)})." : ".");
        }

        if (d.BySetup.Count > 1)
        {
            BreakdownRow top = d.BySetup[0];
            BreakdownRow bottom = d.BySetup[^1];
            yield return $"En çok kazandıran setup: {top.Label} ({Money(top.NetProfit, d.Currency)}, {top.Trades} işlem)." +
                         (bottom.NetProfit < 0 ? $" En çok kaybettiren: {bottom.Label} ({Money(bottom.NetProfit, d.Currency)})." : "");
        }

        BreakdownRow? weakSession = d.BySession.Where(x => x.NetProfit < 0).MinBy(x => x.NetProfit);
        if (weakSession is not null)
            yield return $"{weakSession.Label} seansı bu ay zararda: {weakSession.Trades} işlemde {Money(weakSession.NetProfit, d.Currency)}.";

        ComplianceSummary c = d.Compliance;
        if (c.Tracked > 0)
        {
            string text = $"Kontrol listesi doldurulan {c.Tracked} işlemin %{c.FollowRate:0}'inde tüm kurallara uydun.";
            if (c.Followed.TotalTrades > 0 && c.Broken.TotalTrades > 0)
                text += $" Uyduğunda ortalama {R(c.Followed.AverageR)}, kural çiğnediğinde {R(c.Broken.AverageR)}.";
            if (c.Rules.Count > 0)
                text += $" En çok çiğnenen kural: \"{c.Rules[0].Rule}\" ({c.Rules[0].Broken} kez).";
            yield return text;
        }

        // Günlük notlar: plan yazılan günlerle yazılmayan günlerin sonucu.
        if (d.Notes.Count > 0 && d.Days.Count > 0)
        {
            HashSet<DateTime> planned = d.Notes.Where(n => !string.IsNullOrWhiteSpace(n.Plan)).Select(n => n.Date).ToHashSet();
            List<DailyResult> withPlan = d.Days.Where(x => planned.Contains(x.Date)).ToList();
            List<DailyResult> withoutPlan = d.Days.Where(x => !planned.Contains(x.Date)).ToList();
            if (withPlan.Count > 0 && withoutPlan.Count > 0)
            {
                yield return $"Plan yazdığın {withPlan.Count} işlem gününde net {Money(withPlan.Sum(x => x.NetPnL), d.Currency)}, " +
                             $"plansız {withoutPlan.Count} günde net {Money(withoutPlan.Sum(x => x.NetPnL), d.Currency)}.";
            }

            List<DailyNote> withMood = d.Notes.Where(n => n.Mood.HasValue).ToList();
            if (withMood.Count >= 3)
            {
                decimal average = (decimal)withMood.Average(n => n.Mood!.Value);
                yield return $"Ortalama duygu durumun {average:0.0}/5 ({withMood.Count} gün).";
            }
        }

        if (d.OpenTradeCount > 0)
            yield return $"Ay içinde açılıp hâlâ açık {d.OpenTradeCount} işlem var; bu rapora dahil değil.";
    }

    private static string R(decimal? r) => r is decimal v ? v.ToString("+0.00;-0.00;0.00", Turkish) + "R" : "—";

    public static string Money(decimal value, string currency)
    {
        string number = value.ToString("+#,##0.00;-#,##0.00;0.00", Turkish);
        return string.IsNullOrEmpty(currency) ? number : $"{number} {currency}";
    }
}

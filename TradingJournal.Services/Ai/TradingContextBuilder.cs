using System.Globalization;
using System.Text;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.Services.Analytics;
using TradingJournal.Services.Statistics;

namespace TradingJournal.Services.Ai;

// İşlemleri yapay zekaya gönderilecek kısa ve düzenli bir metne çevirir.
// Modele ham veritabanı yerine hazır hesaplanmış istatistikler verilir: hem daha az token harcanır,
// hem de model hesap hatası yapmaz; görevi sadece yorumlamak olur.
// Kişisel bilgi (hesap adı dışında) gönderilmez.
public class TradingContextBuilder
{
    private const int RecentTradeCount = 40;
    private static readonly CultureInfo Tr = new("tr-TR");

    private readonly IStatisticsService _statistics;
    private readonly TradeAnalyticsService _analytics;

    public TradingContextBuilder(IStatisticsService? statistics = null)
    {
        _statistics = statistics ?? new StatisticsService();
        _analytics = new TradeAnalyticsService(_statistics);
    }

    public string Build(IReadOnlyCollection<Trade> trades, string scopeName)
    {
        var sb = new StringBuilder();
        List<Trade> closed = trades.Where(t => t.IsClosed).ToList();
        TradeStatistics s = _statistics.Calculate(closed);
        BehaviorStatistics b = _analytics.CalculateBehavior(trades);

        sb.AppendLine($"KAPSAM: {scopeName}. Bugün: {DateTime.Now:dd.MM.yyyy}. Saatler Türkiye saati; seanslar New York saatine göre hesaplandı.");
        sb.AppendLine($"Toplam {trades.Count} işlem ({closed.Count} kapanmış, {trades.Count - closed.Count} açık).");
        sb.AppendLine();

        sb.AppendLine("GENEL PERFORMANS");
        sb.AppendLine($"Win rate %{s.WinRate:0.#} ({s.Wins} kazanç / {s.Losses} kayıp), profit factor {Fmt(s.ProfitFactor)}, " +
                      $"ortalama {R(s.AverageR)}, işlem başı beklenti {s.Expectancy:0.##}, net {s.NetProfit:0.##}, " +
                      $"max drawdown {s.MaxDrawdown:0.##}, ortalama kazanç {s.AverageWin:0.##}, ortalama kayıp {s.AverageLoss:0.##}.");
        sb.AppendLine();

        sb.AppendLine("DAVRANIŞ");
        sb.AppendLine($"Ortalama işlemde kalma {Duration(b.AverageHoldTime)} (kazananlarda {Duration(b.AverageWinnerHoldTime)}, " +
                      $"kaybedenlerde {Duration(b.AverageLoserHoldTime)}). Ortalama risk %{Fmt(b.AverageRiskPercent)} " +
                      $"(tutar {b.AverageRiskAmount:0.##}), planlanan ortalama RR {Fmt(b.AveragePlannedRR)}. " +
                      $"En uzun kazanç serisi {b.MaxWinStreak}, kayıp serisi {b.MaxLossStreak}, şu anki seri {b.CurrentStreak:+0;-0;0}.");
        sb.AppendLine();

        AppendGroup(sb, "SEANS × YÖN", closed, t => $"{SessionClassifier.GetSession(t.OpenTime).ToTurkish()} {Dir(t)}");
        AppendGroup(sb, "KILLZONE", closed, t => SessionClassifier.GetKillzone(t.OpenTime).ToTurkish());
        AppendGroup(sb, "HAFTANIN GÜNÜ", closed, t => Tr.DateTimeFormat.GetDayName(t.OpenTime.DayOfWeek));
        AppendGroup(sb, "SEMBOL", closed, t => t.Instrument?.Symbol ?? "?");
        AppendGroup(sb, "STRATEJİ", closed, t => t.Strategy?.Name ?? "(stratejisiz)");
        AppendGroup(sb, "İŞLEMDE KALMA SÜRESİ", closed, t => HoldBucket(t));
        AppendGroup(sb, "UYGULAMA PUANI (1-5 yıldız)", closed, t => t.Rating is int r ? $"{r} yıldız" : "puansız");

        // Etiketler çoka-çok: bir işlem birden fazla etikette sayılır.
        var tagGroups = closed.SelectMany(t => t.Tags.Select(tag => (Tag: tag.Name, Trade: t)))
            .GroupBy(x => x.Tag)
            .Select(g => (g.Key, Stats: _statistics.Calculate(g.Select(x => x.Trade))))
            .OrderByDescending(g => g.Stats.TotalTrades);
        sb.AppendLine("ETİKETLER");
        foreach (var (tag, stats) in tagGroups)
            sb.AppendLine($"- {tag}: {Line(stats)}");
        sb.AppendLine();

        List<EdgeResult> edges = _analytics.FindEdges(closed, minTrades: 3).OrderByDescending(e => e.AverageR).ToList();
        sb.AppendLine("SEMBOL × SEANS × YÖN (en az 3 işlem, en iyiden en kötüye)");
        foreach (EdgeResult e in edges.Take(12).Concat(edges.Skip(Math.Max(12, edges.Count - 8))))
            sb.AppendLine($"- {e.Label}: {e.Trades} işlem, win rate %{e.WinRate:0}, ortalama {R(e.AverageR)}, net {e.NetProfit:0.##}");
        sb.AppendLine();

        sb.AppendLine($"SON {RecentTradeCount} İŞLEM (yeniden eskiye)");
        foreach (Trade t in trades.OrderByDescending(t => t.OpenTime).Take(RecentTradeCount))
            sb.AppendLine(DescribeTrade(t));

        return sb.ToString();
    }

    private void AppendGroup(StringBuilder sb, string title, IEnumerable<Trade> closed, Func<Trade, string> key)
    {
        sb.AppendLine(title);
        foreach (GroupStatistics<string> g in _statistics.GroupBy(closed, key))
            sb.AppendLine($"- {g.Key}: {Line(g.Stats)}");
        sb.AppendLine();
    }

    private static string Line(TradeStatistics s)
        => $"{s.TotalTrades} işlem, win rate %{s.WinRate:0}, ortalama {R(s.AverageR)}, net {s.NetProfit:0.##}";

    private static string DescribeTrade(Trade t)
    {
        string result = t.IsClosed ? $"{R(t.RMultiple)}, net {t.NetPnL:0.##}" : "AÇIK";
        string hold = t.CloseTime is DateTime c ? Duration(c - t.OpenTime) : "-";
        string tags = t.Tags.Count == 0 ? "" : $", etiketler: {string.Join("/", t.Tags.Select(x => x.Name))}";
        string note = string.IsNullOrWhiteSpace(t.Notes) ? "" : $", not: \"{Truncate(t.Notes, 140)}\"";
        return $"- {t.OpenTime:dd.MM HH:mm} {t.Instrument?.Symbol} {Dir(t)}, {SessionClassifier.GetSession(t.OpenTime).ToTurkish()}" +
               $" / {SessionClassifier.GetKillzone(t.OpenTime).ToTurkish()}, {result}, süre {hold}" +
               $", strateji {t.Strategy?.Name ?? "-"}{(t.Rating is int r ? $", puan {r}/5" : "")}{tags}{note}";
    }

    private static string Dir(Trade t) => t.Direction == TradeDirection.Long ? "Long" : "Short";
    private static string R(decimal? r) => r is decimal v ? $"{v:+0.00;-0.00;0.00}R" : "-";
    private static string Fmt(decimal? v) => v is decimal d ? d.ToString("0.##") : "-";

    private static string Duration(TimeSpan? span) => span switch
    {
        null => "-",
        TimeSpan s when s.TotalDays >= 1 => $"{s.TotalDays:0.#} gün",
        TimeSpan s when s.TotalHours >= 1 => $"{s.TotalHours:0.#} saat",
        TimeSpan s => $"{Math.Max(0, s.TotalMinutes):0} dk"
    };

    private static string HoldBucket(Trade t)
    {
        double minutes = (t.CloseTime!.Value - t.OpenTime).TotalMinutes;
        return minutes switch
        {
            < 15 => "15 dk'dan kısa",
            < 60 => "15-60 dk",
            < 240 => "1-4 saat",
            < 1440 => "4-24 saat",
            _ => "1 günden uzun"
        };
    }

    private static string Truncate(string text, int max)
    {
        string single = text.Replace('\n', ' ').Replace('\r', ' ').Replace('"', '\'');
        return single.Length <= max ? single : single[..max] + "…";
    }
}

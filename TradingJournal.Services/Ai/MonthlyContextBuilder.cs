using System.Globalization;
using System.Text;
using TradingJournal.Core.Entities;
using TradingJournal.Services.Analytics;
using TradingJournal.Services.Reports;
using TradingJournal.Services.Statistics;

namespace TradingJournal.Services.Ai;

// Aylık rapor verisini yapay zekaya gönderilecek düz metne çevirir.
// Model kendi hesap yapmasın diye tüm sayılar hazır hesaplanmış olarak verilir.
public static class MonthlyContextBuilder
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string Build(MonthlyReportData d)
    {
        var sb = new StringBuilder();
        TradeStatistics s = d.Stats, p = d.PreviousStats;

        sb.AppendLine($"AY: {MonthlyReportService.MonthName(d.Month)} {d.Month.Year} · HESAP: {d.AccountLabel} · PARA BİRİMİ: {(d.Currency == "" ? "karışık" : d.Currency)}");
        sb.AppendLine($"ÖZET: {s.TotalTrades} kapanan işlem, win rate %{s.WinRate:0.#}, net {Money(s.NetProfit, d)}, " +
                      $"getiri %{d.ReturnPercent:0.00}, profit factor {s.ProfitFactor?.ToString("0.00", Turkish) ?? "-"}, " +
                      $"ortalama R {R(s.AverageR)}, max drawdown {Money(s.MaxDrawdown, d)}, en büyük kazanç {Money(s.LargestWin, d)}, en büyük kayıp {Money(s.LargestLoss, d)}");
        sb.AppendLine(p.TotalTrades == 0
            ? "GEÇEN AY: işlem yok"
            : $"GEÇEN AY: {p.TotalTrades} işlem, win rate %{p.WinRate:0.#}, net {Money(p.NetProfit, d)}, ortalama R {R(p.AverageR)}");
        sb.AppendLine($"GÜNLER: {d.TradingDays} işlem günü, {d.Days.Count(x => x.NetPnL > 0)} kârlı gün. Ay içinde açılıp hâlâ açık: {d.OpenTradeCount}");

        AppendBreakdown(sb, "SETUP", d.BySetup, d);
        AppendBreakdown(sb, "SEMBOL", d.BySymbol, d);
        AppendBreakdown(sb, "SEANS", d.BySession, d);
        AppendBreakdown(sb, "GÜN", d.ByWeekday, d);

        ComplianceSummary c = d.Compliance;
        if (c.Tracked > 0)
        {
            sb.AppendLine($"KURAL UYUMU (strateji kontrol listesi): {c.Tracked} işlemde dolduruldu, %{c.FollowRate:0} tam uyum. " +
                          $"Uyulan işlemler: {c.Followed.TotalTrades} işlem, ort. {R(c.Followed.AverageR)}, net {Money(c.Followed.NetProfit, d)}. " +
                          $"Kural çiğnenen işlemler: {c.Broken.TotalTrades} işlem, ort. {R(c.Broken.AverageR)}, net {Money(c.Broken.NetProfit, d)}.");
            foreach (RuleImpact rule in c.Rules.Take(5))
                sb.AppendLine($"  - \"{rule.Rule}\" ({rule.Strategy}): {rule.Broken}/{rule.Tracked} kez çiğnendi; çiğnenince ort. {R(rule.AverageRWhenBroken)}, uyulunca {R(rule.AverageRWhenMet)}");
        }
        else
        {
            sb.AppendLine("KURAL UYUMU: kontrol listesi kullanılmamış.");
        }

        if (d.Notes.Count > 0)
        {
            sb.AppendLine($"GÜNLÜK NOTLAR: {d.Notes.Count} gün not yazılmış. (Kullanıcının kendi sözleri; sadece bağlam olarak kullan.)");
            Dictionary<DateTime, DailyResult> days = d.Days.ToDictionary(x => x.Date);
            foreach (DailyNote note in d.Notes.TakeLast(12))
            {
                string result = days.TryGetValue(note.Date, out DailyResult? day) ? $"{day.Trades} işlem, net {Money(day.NetPnL, d)}" : "işlem yok";
                string mood = note.Mood is int m ? $", duygu {m}/5" : "";
                sb.AppendLine($"  - {note.Date:dd.MM} ({result}{mood})" +
                              (string.IsNullOrWhiteSpace(note.Plan) ? "" : $" | plan: {Short(note.Plan)}") +
                              (string.IsNullOrWhiteSpace(note.Review) ? "" : $" | değerlendirme: {Short(note.Review)}"));
            }
        }
        else
        {
            sb.AppendLine("GÜNLÜK NOTLAR: yazılmamış.");
        }

        sb.AppendLine("OTOMATİK GÖZLEMLER:");
        foreach (string insight in d.Insights)
            sb.AppendLine($"  - {insight}");
        return sb.ToString();
    }

    private static void AppendBreakdown(StringBuilder sb, string title, List<BreakdownRow> rows, MonthlyReportData d)
    {
        if (rows.Count == 0) return;
        sb.AppendLine($"{title}: " + string.Join("; ", rows.Take(6).Select(r =>
            $"{r.Label} {r.Trades} işlem, %{r.WinRate:0} başarı, ort. {R(r.AverageR)}, net {Money(r.NetProfit, d)}")));
    }

    private static string Short(string text)
    {
        string single = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return single.Length <= 220 ? single : single[..220] + "…";
    }

    private static string R(decimal? r) => r is decimal v ? v.ToString("+0.00;-0.00;0.00", Turkish) + "R" : "-";
    private static string Money(decimal value, MonthlyReportData d) => MonthlyReportService.Money(value, d.Currency);
}

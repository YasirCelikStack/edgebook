using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.Services.Statistics;

namespace TradingJournal.UI.Analytics;

// Pivot tablonun veri kaynağı: kapanmış her işlem için bir satır.
// Boyutlar (sembol, seans, saat...) metin, ölçüler (R, PnL, süre...) sayı olarak tutulur.
// Bazı metinler sıralama için numara ile başlar ("1 Pzt", "2) 15-60 dk") çünkü pivot alfabetik sıralar.
public class AnalyticsRow
{
    private static readonly string[] Days = ["1 Pzt", "2 Sal", "3 Çar", "4 Per", "5 Cum", "6 Cmt", "7 Paz"];

    // --- Boyutlar ---
    public string Symbol { get; init; } = string.Empty;
    public string Market { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public string Session { get; init; } = string.Empty;
    public string Killzone { get; init; } = string.Empty;
    public string Weekday { get; init; } = string.Empty;
    public string Hour { get; init; } = string.Empty;
    public string Month { get; init; } = string.Empty;
    public string Strategy { get; init; } = string.Empty;
    public string Account { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public string HoldBucket { get; init; } = string.Empty;
    public string RiskBucket { get; init; } = string.Empty;
    public string Rating { get; init; } = string.Empty;
    public string Tags { get; init; } = string.Empty;

    // --- Ölçüler ---
    public int One { get; init; } = 1;              // Toplanınca işlem sayısı
    public decimal Win { get; init; }              // 1 / 0: ortalaması win rate
    public decimal R { get; init; }
    public decimal NetPnL { get; init; }
    public decimal HoldMinutes { get; init; }
    public decimal RiskAmount { get; init; }
    public decimal RiskPercent { get; init; }
    public decimal? PlannedRR { get; init; }

    public static AnalyticsRow From(Trade t)
    {
        decimal risk = t.CalculateRiskAmount();
        decimal riskPct = t.Account is { InitialBalance: > 0 } a ? risk / a.InitialBalance * 100m : 0;
        decimal minutes = t.CloseTime is DateTime c ? (decimal)Math.Max(0, (c - t.OpenTime).TotalMinutes) : 0;
        decimal pnl = t.NetPnL ?? 0;

        return new AnalyticsRow
        {
            Symbol = t.Instrument?.Symbol ?? "?",
            Market = t is CryptoTrade ? "Kripto" : "Forex",
            Direction = t.Direction == TradeDirection.Long ? "Long" : "Short",
            Session = SessionClassifier.GetSession(t.OpenTime).ToTurkish(),
            Killzone = SessionClassifier.GetKillzone(t.OpenTime).ToTurkish(),
            Weekday = Days[((int)t.OpenTime.DayOfWeek + 6) % 7],
            Hour = $"{t.OpenTime.Hour:00}:00",
            Month = t.OpenTime.ToString("yyyy-MM"),
            Strategy = t.Strategy?.Name ?? "(stratejisiz)",
            Account = t.Account?.Name ?? "?",
            Outcome = pnl > 0 ? "Kazanç" : pnl < 0 ? "Kayıp" : "Başa baş",
            HoldBucket = minutes switch
            {
                < 15 => "1) 15 dk'dan kısa",
                < 60 => "2) 15-60 dk",
                < 240 => "3) 1-4 saat",
                < 1440 => "4) 4-24 saat",
                _ => "5) 1 günden uzun"
            },
            RiskBucket = riskPct switch
            {
                < 0.5m => "1) %0,5'ten az",
                < 1m => "2) %0,5-1",
                < 2m => "3) %1-2",
                _ => "4) %2 ve üstü"
            },
            Rating = t.Rating is int r ? new string('★', r) : "(puansız)",
            Tags = t.Tags.Count == 0 ? "(etiketsiz)" : string.Join(", ", t.Tags.Select(x => x.Name).OrderBy(x => x)),

            Win = pnl > 0 ? 1 : 0,
            R = t.RMultiple ?? 0,
            NetPnL = pnl,
            HoldMinutes = Math.Round(minutes, 1),
            RiskAmount = Math.Round(risk, 2),
            RiskPercent = Math.Round(riskPct, 2),
            PlannedRR = t.TakeProfit is decimal tp && t.EntryPrice != t.StopLoss
                ? Math.Round(Math.Abs(tp - t.EntryPrice) / Math.Abs(t.EntryPrice - t.StopLoss), 2)
                : null
        };
    }
}

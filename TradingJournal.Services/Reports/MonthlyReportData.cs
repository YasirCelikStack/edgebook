using TradingJournal.Core.Entities;
using TradingJournal.Services.Analytics;
using TradingJournal.Services.Statistics;

namespace TradingJournal.Services.Reports;

// Aylık raporun ihtiyaç duyduğu her şey, hesaplanmış halde.
// Rapor tasarımı (UI katmanı) hiçbir hesap yapmaz; sadece bu nesnedeki değerleri yerleştirir.
// Böylece hesaplar test edilebilir ve aynı veri ileride başka bir çıktıya (ör. e-posta) da verilebilir.
public class MonthlyReportData
{
    public required DateTime Month { get; init; }              // Ayın ilk günü
    public required string AccountLabel { get; init; }         // "Bem 5k İşlemleri" veya "Tüm hesaplar (3)"
    public required string Currency { get; init; }            // "USD"; karışık para birimlerinde boş
    public DateTime GeneratedAt { get; init; } = DateTime.Now;

    public required TradeStatistics Stats { get; init; }
    public required TradeStatistics PreviousStats { get; init; }  // Bir önceki ay (karşılaştırma için)

    public decimal StartBalance { get; init; }                 // Ay başındaki bakiye
    public decimal EndBalance { get; init; }                   // Ay sonundaki bakiye
    public decimal? ReturnPercent => StartBalance == 0 ? null : Math.Round(100m * (EndBalance - StartBalance) / StartBalance, 2);
    public int OpenTradeCount { get; init; }                   // Ay içinde açılıp hâlâ açık olanlar
    public int TradingDays => Days.Count;

    public required List<EquityPoint> Equity { get; init; }    // İlk nokta: ay başı bakiyesi
    public required List<DailyResult> Days { get; init; }
    public required List<BreakdownRow> BySetup { get; init; }
    public required List<BreakdownRow> BySymbol { get; init; }
    public required List<BreakdownRow> BySession { get; init; }
    public required List<BreakdownRow> ByWeekday { get; init; }
    public required List<string> Insights { get; init; }       // Kısa, otomatik gözlemler
    public required List<ReportTradeRow> Trades { get; init; }

    // Strateji kontrol listesine uyum (bu ay kapanan işlemlerde).
    public required ComplianceSummary Compliance { get; init; }

    // Ayın günlük notları (plan / değerlendirme / duygu durumu).
    public required List<DailyNote> Notes { get; init; }

    // İsteğe bağlı yapay zeka koç yorumu; rapor oluşturulmadan önce doldurulur.
    public string? CoachComment { get; set; }
    public string? CoachProvider { get; set; }

    public bool IsEmpty => Stats.TotalTrades == 0;
}

// Takvimdeki tek gün: o gün kapanan işlemlerin toplamı.
public record DailyResult(DateTime Date, decimal NetPnL, int Trades);

// Kırılım tablosundaki satır: "London", "EURUSD", "Salı" gibi bir grup.
public record BreakdownRow(string Label, int Trades, decimal WinRate, decimal NetProfit, decimal? AverageR);

// İşlem listesindeki satır (raporda gösterilecek hazır metin ve değerler).
public class ReportTradeRow
{
    public DateTime OpenTime { get; init; }
    public DateTime? CloseTime { get; init; }
    public required string Symbol { get; init; }
    public required string Direction { get; init; }
    public string Setup { get; init; } = "";
    public string Session { get; init; } = "";
    public decimal EntryPrice { get; init; }
    public decimal? ExitPrice { get; init; }
    public decimal? RMultiple { get; init; }
    public decimal? NetPnL { get; init; }
    public string Tags { get; init; } = "";
    public int? Rating { get; init; }
}

// Rapor alınabilecek bir ay (seçim listesi için).
public record ReportMonth(DateTime Month, int ClosedTrades)
{
    public override string ToString() => $"{MonthlyReportService.MonthName(Month)} {Month.Year}  ·  {ClosedTrades} işlem";
}

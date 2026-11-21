namespace TradingJournal.UI.Analytics;

// Hazır analiz: pivot tablonun hangi alanları satıra, sütuna ve veri alanına koyacağı.
// Alan anahtarları PivotFieldCatalog'daki isimlerdir.
public sealed record AnalysisPreset(string Name, string Description, string[] Rows, string[] Columns, string[] Data)
{
    private static readonly string[] Standard = ["Count", "WinRate", "AvgR", "NetPnL"];

    public static IReadOnlyList<AnalysisPreset> All { get; } =
    [
        new("Seans × Yön",
            "Hangi seansta hangi yönde daha başarılısın?",
            ["Session"], ["Direction"], Standard),

        new("Sembol × Seans × Yön",
            "Örn. Asya'da EURUSD Long işlemlerin başarı oranı. Prop firm'lerin baktığı ana tablo.",
            ["Symbol", "Session"], ["Direction"], ["Count", "WinRate", "AvgR"]),

        new("Killzone",
            "ICT killzone'larına göre performans (New York saatine göre, yaz/kış saati dahil).",
            ["Killzone"], [], ["Count", "WinRate", "AvgR", "NetPnL", "AvgHold"]),

        new("Gün × Saat",
            "Haftanın hangi günü, günün hangi saatinde kazanıyorsun? (Türkiye saati)",
            ["Weekday"], ["Hour"], ["NetPnL"]),

        new("İşlemde kalma süresi",
            "İşlemi ne kadar tuttuğun sonucu etkiliyor mu? Kazananları erken mi kesiyorsun?",
            ["HoldBucket"], ["Outcome"], ["Count", "AvgR"]),

        new("Risk büyüklüğü",
            "Riski artırdığında performansın değişiyor mu? Ortalama risk ve planlanan RR.",
            ["RiskBucket"], [], ["Count", "WinRate", "AvgR", "AvgRiskPct", "AvgPlannedRR", "NetPnL"]),

        new("Strateji",
            "Hangi setup gerçekten çalışıyor?",
            ["Strategy"], [], ["Count", "WinRate", "AvgR", "NetPnL", "AvgHold", "AvgRiskPct"]),

        new("Aylık",
            "Ay ay gelişimin.",
            ["Month"], [], ["Count", "WinRate", "AvgR", "NetPnL"]),
    ];
}

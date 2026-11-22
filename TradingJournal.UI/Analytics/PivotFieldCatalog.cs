using DevExpress.Data.PivotGrid;
using DevExpress.Utils;
using DevExpress.XtraPivotGrid;

namespace TradingJournal.UI.Analytics;

// Pivot tabloda kullanılabilecek tüm alanlar tek listede. Hazır analizler bu anahtarlarla
// alan seçer; kullanıcı da "Alanlar" penceresinden istediğini sürükleyip bırakabilir.
public static class PivotFieldCatalog
{
    private sealed record Definition(string Key, string Caption, string Column, PivotSummaryType? Summary = null, string? Format = null);

    private static readonly Definition[] Definitions =
    [
        // Boyutlar
        new("Symbol", "Sembol", nameof(AnalyticsRow.Symbol)),
        new("Market", "Piyasa", nameof(AnalyticsRow.Market)),
        new("Direction", "Yön", nameof(AnalyticsRow.Direction)),
        new("Session", "Seans", nameof(AnalyticsRow.Session)),
        new("Killzone", "Killzone", nameof(AnalyticsRow.Killzone)),
        new("Weekday", "Gün", nameof(AnalyticsRow.Weekday)),
        new("Hour", "Saat (TR)", nameof(AnalyticsRow.Hour)),
        new("Month", "Ay", nameof(AnalyticsRow.Month)),
        new("Strategy", "Strateji", nameof(AnalyticsRow.Strategy)),
        new("Compliance", "Kural uyumu", nameof(AnalyticsRow.Compliance)),
        new("Account", "Hesap", nameof(AnalyticsRow.Account)),
        new("Outcome", "Sonuç", nameof(AnalyticsRow.Outcome)),
        new("HoldBucket", "İşlemde kalma", nameof(AnalyticsRow.HoldBucket)),
        new("RiskBucket", "Risk %", nameof(AnalyticsRow.RiskBucket)),
        new("Rating", "Puan", nameof(AnalyticsRow.Rating)),
        new("Tags", "Etiketler", nameof(AnalyticsRow.Tags)),

        // Ölçüler
        new("Count", "İşlem", nameof(AnalyticsRow.One), PivotSummaryType.Sum, "0"),
        new("WinRate", "Win rate", nameof(AnalyticsRow.Win), PivotSummaryType.Average, "p0"),
        new("AvgR", "Ort. R", nameof(AnalyticsRow.R), PivotSummaryType.Average, "+0.00R;-0.00R;0.00R"),
        new("SumR", "Toplam R", nameof(AnalyticsRow.R), PivotSummaryType.Sum, "+0.0R;-0.0R;0R"),
        new("NetPnL", "Net PnL", nameof(AnalyticsRow.NetPnL), PivotSummaryType.Sum, "+#,0.00;-#,0.00;0"),
        new("AvgHold", "Ort. süre (dk)", nameof(AnalyticsRow.HoldMinutes), PivotSummaryType.Average, "#,0 'dk'"),
        new("AvgRisk", "Ort. risk", nameof(AnalyticsRow.RiskAmount), PivotSummaryType.Average, "#,0.00"),
        new("AvgRiskPct", "Ort. risk %", nameof(AnalyticsRow.RiskPercent), PivotSummaryType.Average, "0.00'%'"),
        new("AvgPlannedRR", "Ort. planlanan RR", nameof(AnalyticsRow.PlannedRR), PivotSummaryType.Average, "0.00"),
    ];

    // Renklendirilecek (pozitif yeşil, negatif kırmızı) ölçüler.
    public static readonly HashSet<string> SignedMeasures = ["AvgR", "SumR", "NetPnL"];

    public static Dictionary<string, PivotGridField> CreateFields(PivotGridControl pivot)
    {
        var fields = new Dictionary<string, PivotGridField>();
        foreach (Definition d in Definitions)
        {
            var field = new PivotGridField
            {
                Name = "field" + d.Key,
                Caption = d.Caption,
                Visible = false,
                DataBinding = new DataSourceColumnBinding(d.Column),
                // Başlık düğmesindeki metin (ve sıralama oku) kesilmesin diye en az bu kadar geniş.
                Width = d.Summary is null ? 140 : 100,
                MinWidth = d.Summary is null ? 110 : 80
            };

            if (d.Summary is PivotSummaryType summary)
            {
                field.Area = PivotArea.DataArea;
                field.SummaryType = summary;
                field.CellFormat.FormatType = FormatType.Numeric;
                field.CellFormat.FormatString = d.Format;
            }
            else
            {
                field.Area = PivotArea.RowArea;
            }

            pivot.Fields.Add(field);
            fields[d.Key] = field;
        }
        return fields;
    }

    public static string? KeyOf(PivotGridField? field)
        => field?.Name is string name && name.StartsWith("field") ? name["field".Length..] : null;
}

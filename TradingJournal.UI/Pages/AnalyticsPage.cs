using System.ComponentModel;
using DevExpress.XtraEditors;
using DevExpress.XtraPivotGrid;
using TradingJournal.Core.Entities;
using TradingJournal.Services.Analytics;
using TradingJournal.UI.Analytics;
using TradingJournal.UI.Controls;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Pages;

// Detaylı analiz: DevExpress PivotGridControl ile çapraz tablolar (seans × sembol × yön vb.),
// hazır analiz düğmeleri ve sağda davranış istatistikleri + en güçlü / en zayıf kurulumlar.
[DesignerCategory("Code")]
public class AnalyticsPage : PageBase
{
    private const int MinTradesForEdge = 5;

    private readonly TradeAnalyticsService _analytics = new();
    private readonly PivotGridControl _pivot = new() { Dock = DockStyle.Fill };
    private readonly Dictionary<string, PivotGridField> _fields;
    private readonly List<FlatButton> _presetButtons = new();
    private readonly Label _presetDescription = new()
    {
        Dock = DockStyle.Top, Height = 28, ForeColor = Theme.TextMuted, Font = Theme.Body,
        TextAlign = ContentAlignment.MiddleLeft, BackColor = Theme.Background
    };

    private readonly CardPanel _pivotCard = new("Pivot tablo") { Dock = DockStyle.Fill };
    private readonly RuleComplianceService _compliance = new();
    private readonly CardPanel _complianceCard = new("Kural uyumu") { Hint = "kontrol listesi" };
    private readonly InsightList _complianceList = new() { Dock = DockStyle.Fill };
    private readonly CardPanel _behaviorCard = new("Davranışın");
    private readonly InsightList _behavior = new() { Dock = DockStyle.Fill };
    private readonly CardPanel _bestCard = new("En güçlü kurulumların") { Hint = $"en az {MinTradesForEdge} işlem" };
    private readonly InsightList _best = new() { Dock = DockStyle.Fill };
    private readonly CardPanel _worstCard = new("Zayıf noktaların") { Hint = $"en az {MinTradesForEdge} işlem" };
    private readonly InsightList _worst = new() { Dock = DockStyle.Fill };
    private readonly XtraScrollableControl _side;

    public AnalyticsPage(AppState state) : base(state)
    {
        ConfigurePivot();
        _fields = PivotFieldCatalog.CreateFields(_pivot);

        // Üst satır: hazır analiz düğmeleri + sağda araçlar
        var presets = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Background, WrapContents = true };
        foreach (AnalysisPreset preset in AnalysisPreset.All)
        {
            var button = new FlatButton(preset.Name);
            button.Click += (_, _) => ApplyPreset(preset);
            _presetButtons.Add(button);
            presets.Controls.Add(button);
        }

        var fieldsButton = new FlatButton("Alanlar", "\uE71D");
        fieldsButton.Click += (_, _) => _pivot.FieldsCustomization();
        var exportButton = new FlatButton("Excel'e aktar", "\uE74E");
        exportButton.Click += (_, _) => ExportToExcel();
        var tools = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 250, BackColor = Theme.Background, FlowDirection = FlowDirection.RightToLeft };
        tools.Controls.Add(exportButton);
        tools.Controls.Add(fieldsButton);

        var top = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.Background };
        top.Controls.Add(presets);
        top.Controls.Add(tools);

        _pivotCard.Controls.Add(_pivot);
        _behaviorCard.Controls.Add(_behavior);
        _complianceCard.Controls.Add(_complianceList);
        _bestCard.Controls.Add(_best);
        _worstCard.Controls.Add(_worst);

        // Sağ panel: kartlar alt alta, her biri içeriği kadar uzun; sığmazsa panel kaydırılır.
        _side = new XtraScrollableControl { Dock = DockStyle.Right, Width = 372, BackColor = Theme.Background, Padding = new Padding(0, 0, 4, 0) };
        _side.Appearance.BackColor = Theme.Background;
        // Dock=Top'ta son eklenen en üstte durur: alttan üste doğru ekliyoruz, aralara boşluk koyuyoruz.
        CardPanel[] order = [_worstCard, _bestCard, _behaviorCard, _complianceCard];
        for (int i = 0; i < order.Length; i++)
        {
            order[i].Dock = DockStyle.Top;
            _side.Controls.Add(order[i]);
            if (i < order.Length - 1)
                _side.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Theme.Background });
        }

        Controls.Add(_pivotCard);
        Controls.Add(_side);
        Controls.Add(_presetDescription);
        Controls.Add(top);

        ApplyPreset(AnalysisPreset.All[1]);
    }

    public override string Title => "Analiz";
    public override string Subtitle => "Kapanmış işlemlerin; seans, sembol, yön, saat ve süreye göre kırılımı";

    private void ConfigurePivot()
    {
        // Alanlar DataSourceColumnBinding ile bağlanıyor; bu bağlama sadece Optimized veri motorunda
        // kesin çalışır. Eski (Legacy) motor seçili kalırsa alanlar veriyi göremez ve tablo 0 gösterir.
        _pivot.OptionsData.DataProcessingEngine = PivotDataProcessingEngine.Optimized;
        _pivot.OptionsView.ShowFilterHeaders = false;
        // Sol üstteki ölçü düğmeleri (İşlem, Win rate...) sütun başlıklarında zaten yazıyor; dar alanda "İ..." diye kesiliyorlardı.
        _pivot.OptionsView.ShowDataHeaders = false;
        _pivot.OptionsView.ShowColumnGrandTotals = true;
        _pivot.OptionsView.ShowRowGrandTotals = true;
        _pivot.OptionsView.ShowColumnTotals = true;
        _pivot.OptionsView.ShowRowTotals = true;
        _pivot.OptionsCustomization.AllowDrag = true;
        _pivot.OptionsCustomization.AllowFilter = true;

        // Pozitif sonuçlar yeşil, negatifler kırmızı; win rate %50 üstü yeşil.
        _pivot.CustomAppearance += (_, e) =>
        {
            string? key = PivotFieldCatalog.KeyOf(e.DataField);
            if (key is null || e.Value is null) return;
            decimal value;
            try { value = Convert.ToDecimal(e.Value); }
            catch (Exception) { return; }

            if (PivotFieldCatalog.SignedMeasures.Contains(key))
                e.Appearance.ForeColor = Theme.ForValue(value);
            else if (key == "WinRate")
                e.Appearance.ForeColor = value >= 0.5m ? Theme.Profit : Theme.Loss;
        };
    }

    public override async Task RefreshAsync()
    {
        List<Trade> trades = await State.LoadTradesAsync();
        List<Trade> closed = trades.Where(t => t.IsClosed).ToList();

        _pivot.DataSource = AnalyticsRow.ToDataTable(closed.Select(AnalyticsRow.From));
        _pivot.RefreshData();
        _pivot.BestFit(); // Sütunları içeriğe göre genişlet (başlıklar dahil).
        _pivotCard.Hint = $"{closed.Count} kapanmış işlem";
        _pivotCard.Invalidate();

        UpdateCompliance(closed);
        UpdateBehavior(trades);
        UpdateEdges(closed);
        FitSideCards();
    }

    // ---------------- Hazır analizler ----------------

    private void ApplyPreset(AnalysisPreset preset)
    {
        _pivot.BeginUpdate();
        try
        {
            foreach (PivotGridField field in _fields.Values)
                field.Visible = false;

            Place(preset.Rows, PivotArea.RowArea);
            Place(preset.Columns, PivotArea.ColumnArea);
            Place(preset.Data, PivotArea.DataArea);
        }
        finally
        {
            _pivot.EndUpdate();
        }
        _pivot.BestFit();

        _presetDescription.Text = "   " + preset.Description;
        for (int i = 0; i < _presetButtons.Count; i++)
        {
            _presetButtons[i].Primary = AnalysisPreset.All[i] == preset;
            _presetButtons[i].Invalidate();
        }
    }

    private void Place(string[] keys, PivotArea area)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (!_fields.TryGetValue(keys[i], out PivotGridField? field)) continue;
            field.Area = area;
            field.AreaIndex = i;
            field.Visible = true;
        }
    }

    // ---------------- Sağ panel ----------------

    // Kartın yüksekliği = başlık alanı + satırlar. Böylece alttaki satırlar kesilmez.
    private void FitSideCards()
    {
        _complianceCard.Height = 44 + _complianceList.PreferredHeight + 16;
        _behaviorCard.Height = 44 + _behavior.PreferredHeight + 16;
        _bestCard.Height = 44 + _best.PreferredHeight + 16;
        _worstCard.Height = 44 + _worst.PreferredHeight + 16;
    }

    // Kontrol listesine uyulan ve çiğnenen işlemlerin karşılaştırması + en çok çiğnenen kurallar.
    private void UpdateCompliance(List<Trade> closed)
    {
        ComplianceSummary c = _compliance.Calculate(closed);
        if (c.Tracked == 0)
        {
            _complianceList.SetLines(new List<InsightList.Line>(),
                "Tanımlar > Stratejiler'de bir stratejiye kontrol listesi ekle, işlem girerken uyduğun kuralları işaretle. " +
                "Kurala uyduğun ve uymadığın işlemler burada karşılaştırılır.");
            return;
        }

        static string R(decimal? r) => r is decimal v ? $"{v:+0.00;-0.00;0.00}R" : "-";
        static string Detail(Services.Statistics.TradeStatistics s)
            => s.TotalTrades == 0 ? "işlem yok" : $"%{s.WinRate:0} başarı · {s.TotalTrades} işlem · {s.NetProfit:+#,0;-#,0;0}";

        var lines = new List<InsightList.Line>
        {
            new("Kurallara uyduğunda", R(c.Followed.AverageR), Detail(c.Followed), Theme.ForValue(c.Followed.AverageR)),
            new("Kural çiğnediğinde", R(c.Broken.AverageR), Detail(c.Broken), Theme.ForValue(c.Broken.AverageR)),
            new("Uyum oranı", c.FollowRate is decimal rate ? $"%{rate:0}" : "-",
                $"kontrol listesi doldurulan {c.Tracked} işlemde" + (c.NotTracked > 0 ? $" ({c.NotTracked} işlemde liste yok)" : ""),
                c.FollowRate >= 80 ? Theme.Profit : c.FollowRate >= 50 ? Theme.Warning : Theme.Loss)
        };
        foreach (RuleImpact rule in c.Rules.Take(3))
        {
            lines.Add(new($"⚠ {rule.Rule}", $"{rule.Broken} kez",
                $"çiğnenince {R(rule.AverageRWhenBroken)} · uyulunca {R(rule.AverageRWhenMet)} · {rule.Strategy}",
                Theme.Warning));
        }
        _complianceList.SetLines(lines);
    }

    private void UpdateBehavior(List<Trade> trades)
    {
        BehaviorStatistics b = _analytics.CalculateBehavior(trades);

        static string Duration(TimeSpan? span) => span switch
        {
            null => "-",
            { TotalDays: >= 1 } s => $"{(int)s.TotalDays} g {s.Hours} sa",
            { TotalHours: >= 1 } s => $"{(int)s.TotalHours} sa {s.Minutes} dk",
            TimeSpan s => $"{(int)s.TotalMinutes} dk"
        };

        string streakText = b.CurrentStreak switch
        {
            > 0 => $"{b.CurrentStreak} kazanç üst üste",
            < 0 => $"{-b.CurrentStreak} kayıp üst üste",
            _ => "-"
        };

        // Kazananları kaybedenlerden daha kısa tutmak, "kârı erken kesme" işaretidir.
        string? holdHint = b.AverageWinnerHoldTime is TimeSpan w && b.AverageLoserHoldTime is TimeSpan l && w < l
            ? "Kazananları kaybedenlerden kısa tutuyorsun"
            : null;

        _behavior.SetLines(
        [
            new("Ortalama işlemde kalma", Duration(b.AverageHoldTime)),
            new("Kazananlarda", Duration(b.AverageWinnerHoldTime), holdHint, Theme.Profit),
            new("Kaybedenlerde", Duration(b.AverageLoserHoldTime), null, Theme.Loss),
            new("Ortalama risk", b.AverageRiskAmount.ToString("#,0.00"), "işlem başına, hesap para biriminde"),
            new("Ortalama risk %", b.AverageRiskPercent is decimal p ? $"%{p:0.00}" : "-", "başlangıç bakiyesine göre",
                b.AverageRiskPercent > 2 ? Theme.Warning : null),
            new("Planlanan ortalama RR", b.AveragePlannedRR is decimal rr ? $"1 : {rr:0.00}" : "-"),
            new("En uzun kazanç serisi", b.MaxWinStreak.ToString(), null, Theme.Profit),
            new("En uzun kayıp serisi", b.MaxLossStreak.ToString(), null, Theme.Loss),
            new("Şu anki seri", streakText, null, b.CurrentStreak >= 0 ? Theme.Profit : Theme.Loss),
        ]);
    }

    private void UpdateEdges(List<Trade> closed)
    {
        List<EdgeResult> edges = _analytics.FindEdges(closed, MinTradesForEdge);

        static InsightList.Line ToLine(EdgeResult e) => new(
            e.Label,
            $"{e.AverageR:+0.00;-0.00;0.00}R",
            $"%{e.WinRate:0} başarı · {e.Trades} işlem · {e.NetProfit:+#,0;-#,0;0}",
            Theme.ForValue(e.AverageR));

        string empty = $"Bir sembol + seans + yön kombinasyonunda en az {MinTradesForEdge} kapanmış işlem olunca burada görünür.";
        _best.SetLines(edges.Where(e => e.AverageR > 0).OrderByDescending(e => e.AverageR).Take(5).Select(ToLine).ToList(), empty);
        _worst.SetLines(edges.Where(e => e.AverageR < 0).OrderBy(e => e.AverageR).Take(5).Select(ToLine).ToList(),
            edges.Count == 0 ? empty : "Negatif beklentili kombinasyon yok 👏");
    }

    private void ExportToExcel()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Pivot tabloyu Excel'e aktar",
            Filter = "Excel dosyası|*.xlsx",
            FileName = $"EdgeBook-analiz-{DateTime.Now:yyyy-MM-dd}.xlsx"
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            _pivot.ExportToXlsx(dialog.FileName);
            XtraMessageBox.Show(FindForm(), "Excel dosyası kaydedildi.", "EdgeBook", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
        }
    }
}

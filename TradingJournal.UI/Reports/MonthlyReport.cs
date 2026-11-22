using System.ComponentModel;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.Utils;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using TradingJournal.Services.Analytics;
using TradingJournal.Services.Reports;
using TradingJournal.Services.Statistics;
using TradingJournal.UI.Helpers;
using Charts = DevExpress.XtraCharts;

namespace TradingJournal.UI.Reports;

// Aylık performans raporu (DevExpress XtraReports), tasarımcı kullanılmadan kodla kurulur.
// Kağıda basılacağı için uygulamanın koyu teması değil, açık ve sade bir tema kullanılır.
//
//   Sayfa 1: başlık, 6 KPI kutusu, bakiye, equity curve, günlük kâr/zarar, otomatik gözlemler, (varsa) koç yorumu
//   Sayfa 2: setup / sembol / seans / gün kırılımları, kural uyumu (kontrol listesi)
//   Sayfa 3+: ayın tüm işlemleri (başlık satırı her sayfada tekrar eder)
//
// Rapor hiçbir hesap yapmaz; tüm değerler MonthlyReportService'in hazırladığı MonthlyReportData'dan gelir.
[DesignerCategory("Code")]
public class MonthlyReport : XtraReport
{
    // A4 (827 x 1169, inçin yüzde biri) - 40'lık kenar boşlukları
    private const float ContentWidth = 747f;
    private const float Gap = 12f;

    private static readonly Color Ink = Color.FromArgb(17, 24, 39);
    private static readonly Color Muted = Color.FromArgb(107, 114, 128);
    private static readonly Color Faint = Color.FromArgb(156, 163, 175);
    private static readonly Color Accent = Color.FromArgb(56, 132, 255);
    private static readonly Color Profit = Color.FromArgb(22, 163, 74);
    private static readonly Color Loss = Color.FromArgb(220, 38, 38);
    private static readonly Color TileBack = Color.FromArgb(244, 247, 251);
    private static readonly Color HeaderBack = Color.FromArgb(235, 240, 248);
    private static readonly Color Zebra = Color.FromArgb(249, 250, 252);
    private static readonly Color Line = Color.FromArgb(226, 230, 236);

    private readonly MonthlyReportData _data;
    private string Currency => _data.Currency;

    public MonthlyReport(MonthlyReportData data)
    {
        _data = data;
        string period = $"{MonthlyReportService.MonthName(data.Month)} {data.Month.Year}";

        PaperKind = DXPaperKind.A4;
        Margins = new DXMargins(40, 40, 40, 40);
        Font = F(9);
        DisplayName = $"EdgeBook {period} raporu - {data.AccountLabel}";
        DataSource = data.Trades;

        var top = new TopMarginBand { HeightF = 40 };
        var bottom = new BottomMarginBand { HeightF = 40 };
        BuildFooter(bottom, period);

        var header = new ReportHeaderBand();
        float y = BuildTitle(header, period);
        y = BuildKpis(header, y + 16);
        y = BuildEquity(header, y + 18);
        y = BuildDaily(header, y + 18);
        y = BuildInsights(header, y + 18);
        if (!string.IsNullOrWhiteSpace(data.CoachComment))
            y = BuildCoachComment(header, y + 14);

        // Kırılımlar yeni sayfadan başlar.
        header.Controls.Add(new XRPageBreak { LocationF = new PointF(0, y + 4) });
        y = BuildBreakdowns(header, y + 8);
        if (data.Compliance.Tracked > 0)
            y = BuildCompliance(header, y + 24);
        header.HeightF = y + 4;

        Bands.Add(top);
        Bands.Add(header);
        if (data.Trades.Count > 0)
        {
            (GroupHeaderBand listHeader, DetailBand detail) = BuildTradeList();
            Bands.Add(listHeader);
            Bands.Add(detail);
        }
        Bands.Add(bottom);
    }

    // ---------------- Bölümler ----------------

    private float BuildTitle(Band band, string period)
    {
        if (AppIcon.Logo is Image logo)
        {
            band.Controls.Add(new XRPictureBox
            {
                LocationF = new PointF(0, 2),
                SizeF = new SizeF(42, 42),
                ImageSource = new ImageSource(logo),
                Sizing = ImageSizeMode.ZoomImage
            });
        }

        band.Controls.Add(Label("EDGEBOOK  ·  AYLIK PERFORMANS RAPORU", 54, 0, 450, 16, F(7.5f, bold: true), Accent));
        band.Controls.Add(Label(period, 54, 14, 450, 30, F(18, bold: true), Ink));
        band.Controls.Add(Label(_data.AccountLabel, 54, 42, 450, 18, F(10), Muted));
        band.Controls.Add(Label($"Oluşturma: {_data.GeneratedAt:dd.MM.yyyy HH:mm}", ContentWidth - 260, 14, 260, 16, F(8), Faint, TextAlignment.MiddleRight));

        string balance = $"Bakiye  {Plain(_data.StartBalance)}  →  {Plain(_data.EndBalance)}";
        band.Controls.Add(Label(balance, ContentWidth - 300, 34, 300, 18, F(9, bold: true), Ink, TextAlignment.MiddleRight));

        band.Controls.Add(new XRPanel
        {
            LocationF = new PointF(0, 68),
            SizeF = new SizeF(ContentWidth, 3),
            BackColor = Accent,
            Borders = BorderSide.None
        });
        return 71;
    }

    private float BuildKpis(Band band, float y)
    {
        TradeStatistics s = _data.Stats, p = _data.PreviousStats;
        bool hasPrevious = p.TotalTrades > 0;
        string Prev(string text) => hasPrevious ? $"Geçen ay: {text}" : "Geçen ay işlem yok";

        var tiles = new (string Caption, string Value, Color Color, string Sub)[]
        {
            ("NET KÂR / ZARAR", MonthlyReportService.Money(s.NetProfit, Currency), ColorFor(s.NetProfit),
                _data.ReturnPercent is decimal r ? $"Getiri: %{r:+0.00;-0.00;0.00}" : ""),
            ("WIN RATE", $"%{s.WinRate:0.#}", Ink, $"{s.Wins} kazanç · {s.Losses} kayıp"),
            ("PROFIT FACTOR", s.ProfitFactor?.ToString("0.00") ?? "—", s.ProfitFactor >= 1 ? Profit : s.ProfitFactor is null ? Ink : Loss,
                Prev(p.ProfitFactor?.ToString("0.00") ?? "—")),
            ("ORTALAMA R", s.AverageR?.ToString("+0.00;-0.00;0.00") ?? "—", ColorFor(s.AverageR),
                Prev(p.AverageR?.ToString("+0.00;-0.00;0.00") ?? "—")),
            ("MAX DRAWDOWN", Plain(s.MaxDrawdown), s.MaxDrawdown > 0 ? Loss : Ink,
                _data.StartBalance > 0 ? $"Ay başı bakiyenin %{100m * s.MaxDrawdown / _data.StartBalance:0.0}'i" : ""),
            ("İŞLEM", $"{s.TotalTrades}", Ink, $"{_data.TradingDays} işlem günü" + (hasPrevious ? $" · geçen ay {p.TotalTrades}" : ""))
        };

        const int perRow = 3;
        const float height = 66;
        float width = (ContentWidth - Gap * (perRow - 1)) / perRow;
        for (int i = 0; i < tiles.Length; i++)
        {
            float x = (i % perRow) * (width + Gap);
            float ty = y + (i / perRow) * (height + Gap);
            var tile = new XRPanel
            {
                LocationF = new PointF(x, ty),
                SizeF = new SizeF(width, height),
                BackColor = TileBack,
                Borders = BorderSide.None
            };
            tile.Controls.Add(Label(tiles[i].Caption, 12, 8, width - 24, 14, F(7, bold: true), Muted));
            tile.Controls.Add(Label(tiles[i].Value, 12, 22, width - 24, 24, F(14, bold: true), tiles[i].Color));
            tile.Controls.Add(Label(tiles[i].Sub, 12, 46, width - 24, 14, F(7.5f), Faint));
            band.Controls.Add(tile);
        }
        return y + 2 * height + Gap;
    }

    private float BuildEquity(Band band, float y)
    {
        y = SectionTitle(band, y, "Equity curve", "Her kapanan işlemden sonraki bakiye");
        XRChart chart = Chart(y, 200);

        // Altta yarı saydam alan, üstte belirgin çizgi.
        var area = new Charts.Series("Bakiye alanı", Charts.ViewType.Area) { ArgumentScaleType = Charts.ScaleType.DateTime };
        var line = new Charts.Series("Bakiye", Charts.ViewType.Line) { ArgumentScaleType = Charts.ScaleType.DateTime };
        foreach (EquityPoint point in _data.Equity)
        {
            area.Points.Add(new Charts.SeriesPoint(point.Time, (double)point.Balance));
            line.Points.Add(new Charts.SeriesPoint(point.Time, (double)point.Balance));
        }
        area.LabelsVisibility = DefaultBoolean.False;
        line.LabelsVisibility = DefaultBoolean.False;
        var areaView = (Charts.AreaSeriesView)area.View;
        areaView.Color = Accent;
        areaView.Transparency = 200;
        areaView.MarkerVisibility = DefaultBoolean.False;
        var lineView = (Charts.LineSeriesView)line.View;
        lineView.Color = Accent;
        lineView.LineStyle.Thickness = 2;
        lineView.MarkerVisibility = DefaultBoolean.False;

        chart.Series.Add(area);
        chart.Series.Add(line);
        if (chart.Diagram is Charts.XYDiagram diagram)
        {
            StyleDiagram(diagram);
            // Sürekli ölçek: aynı güne düşen işlemler toplanmaz, her biri ayrı nokta olarak çizilir.
            diagram.AxisX.DateTimeScaleOptions.ScaleMode = Charts.ScaleMode.Continuous;
            diagram.AxisX.Label.TextPattern = "{A:dd.MM}";
            // Eksen sıfırdan başlamasın: bakiyenin oynadığı aralık (biraz boşlukla) gösterilsin.
            double min = (double)_data.Equity.Min(p => p.Balance);
            double max = (double)_data.Equity.Max(p => p.Balance);
            double pad = Math.Max((max - min) * 0.1, Math.Max(Math.Abs(max) * 0.01, 1));
            diagram.AxisY.WholeRange.SetMinMaxValues(min - pad, max + pad);
            diagram.AxisY.VisualRange.SetMinMaxValues(min - pad, max + pad);
            diagram.AxisY.Label.TextPattern = "{V:#,##0}";
        }
        band.Controls.Add(chart);
        return y + 200;
    }

    private float BuildDaily(Band band, float y)
    {
        y = SectionTitle(band, y, "Günlük kâr / zarar", "O gün kapanan işlemlerin toplamı");
        XRChart chart = Chart(y, 150);

        // Kâr ve zarar iki ayrı "yığılmış" seri: her gün tek çubuk olur, rengi işaretine göre değişir.
        var gains = new Charts.Series("Kâr", Charts.ViewType.StackedBar) { ArgumentScaleType = Charts.ScaleType.DateTime };
        var losses = new Charts.Series("Zarar", Charts.ViewType.StackedBar) { ArgumentScaleType = Charts.ScaleType.DateTime };
        foreach (DailyResult day in _data.Days)
        {
            (day.NetPnL >= 0 ? gains : losses).Points.Add(new Charts.SeriesPoint(day.Date, (double)day.NetPnL));
        }
        foreach ((Charts.Series series, Color color) in new[] { (gains, Profit), (losses, Loss) })
        {
            series.LabelsVisibility = DefaultBoolean.False;
            var view = (Charts.StackedBarSeriesView)series.View;
            view.Color = color;
            view.BarWidth = 0.65;
            view.FillStyle.FillMode = Charts.FillMode.Solid;
            chart.Series.Add(series);
        }

        if (chart.Diagram is Charts.XYDiagram diagram)
        {
            StyleDiagram(diagram);
            diagram.AxisX.DateTimeScaleOptions.MeasureUnit = Charts.DateTimeMeasureUnit.Day;
            diagram.AxisX.DateTimeScaleOptions.GridAlignment = Charts.DateTimeGridAlignment.Week;
            diagram.AxisX.Label.TextPattern = "{A:dd.MM}";
            diagram.AxisY.Label.TextPattern = "{V:#,##0}";
            // Ayın tamamı görünsün: işlem olmayan günler boşluk olarak kalır.
            diagram.AxisX.WholeRange.SetMinMaxValues(_data.Month, _data.Month.AddMonths(1).AddDays(-1));
            diagram.AxisX.VisualRange.SetMinMaxValues(_data.Month, _data.Month.AddMonths(1).AddDays(-1));
        }
        band.Controls.Add(chart);
        return y + 150;
    }

    private float BuildInsights(Band band, float y)
    {
        y = SectionTitle(band, y, "Bu ayın özeti", "Verilerden otomatik çıkarıldı");
        foreach (string insight in _data.Insights)
        {
            band.Controls.Add(Label("•", 0, y, 12, 18, F(9, bold: true), Accent));
            XRLabel text = Label(insight, 14, y, ContentWidth - 14, 18, F(9), Ink);
            text.WordWrap = true;
            text.CanGrow = true; // Uzun satır ikinci satıra geçer, alttakiler kendiliğinden aşağı kayar.
            band.Controls.Add(text);
            y += 22;
        }
        return y;
    }

    // Yapay zeka koç yorumu: açık mavi zeminli, uzunluğuna göre büyüyen tek blok.
    private float BuildCoachComment(Band band, float y)
    {
        y = SectionTitle(band, y, "Koç yorumu", $"Yapay zeka ({_data.CoachProvider ?? "Gemini"}) · rapordaki verilere göre");
        XRLabel comment = Label(_data.CoachComment!, 0, y, ContentWidth, 40, F(9), Ink);
        comment.BackColor = TileBack;
        comment.Padding = new PaddingInfo(12, 12, 10, 10);
        comment.WordWrap = true;
        comment.CanGrow = true;
        comment.TextAlignment = TextAlignment.TopLeft;
        comment.Borders = BorderSide.Left;
        comment.BorderColor = Accent;
        comment.BorderWidth = 3;
        band.Controls.Add(comment);
        return y + 44;
    }

    // Kontrol listesi: uyulan / çiğnenen işlemlerin karşılaştırması ve en çok çiğnenen kurallar.
    private float BuildCompliance(Band band, float y)
    {
        ComplianceSummary c = _data.Compliance;
        y = SectionTitle(band, y, "Kural uyumu", $"Kontrol listesi doldurulan {c.Tracked} işlem · tam uyum %{c.FollowRate:0}");
        float width = (ContentWidth - Gap) / 2;

        BreakdownRow Row(string label, TradeStatistics s) => new(label, s.TotalTrades, s.WinRate, s.NetProfit, s.AverageR);
        var summary = new List<BreakdownRow> { Row("Kurallara uyuldu", c.Followed), Row("Kural çiğnendi", c.Broken) };
        if (c.NotTracked > 0)
            summary.Add(new BreakdownRow("Liste doldurulmadı", c.NotTracked, 0, 0, null));
        float left = BreakdownTable(band, 0, y, width, "Uyum", summary);

        var rules = c.Rules.Take(8).Select(r => (
            Cells: new[] { r.Rule, $"{r.Broken}/{r.Tracked}", r.AverageRWhenBroken?.ToString("+0.00;-0.00;0.00") ?? "—", r.AverageRWhenMet?.ToString("+0.00;-0.00;0.00") ?? "—" },
            Colors: new Color?[] { null, null, ColorFor(r.AverageRWhenBroken), ColorFor(r.AverageRWhenMet) })).ToList();
        if (rules.Count == 0)
            rules.Add((new[] { "Hiç kural çiğnenmemiş 👏", "", "", "" }, new Color?[4]));
        float right = Table(band, width + Gap, y, width,
            ["Çiğnenen kural", "Çiğnendi", "Çiğnenince R", "Uyulunca R"],
            [2.6f, 0.9f, 1.1f, 1.1f],
            [TextAlignment.MiddleLeft, TextAlignment.MiddleRight, TextAlignment.MiddleRight, TextAlignment.MiddleRight],
            rules);
        return Math.Max(left, right);
    }

    private float BuildBreakdowns(Band band, float y)
    {
        y = SectionTitle(band, y, "Kırılımlar", "Kapanan işlemler, net kâra göre sıralı (gün tablosu haftanın sırasıyla)");
        float width = (ContentWidth - Gap) / 2;

        float left = BreakdownTable(band, 0, y, width, "Setup", _data.BySetup);
        float right = BreakdownTable(band, width + Gap, y, width, "Sembol", _data.BySymbol);
        y = Math.Max(left, right) + 20;

        left = BreakdownTable(band, 0, y, width, "Seans", _data.BySession);
        right = BreakdownTable(band, width + Gap, y, width, "Gün", _data.ByWeekday);
        return Math.Max(left, right);
    }

    private float BreakdownTable(Band band, float x, float y, float width, string title, List<BreakdownRow> rows)
    {
        const int maxRows = 8;
        var lines = rows.Take(maxRows).Select(row => (
            Cells: new[] { row.Label, row.Trades.ToString(), row.Trades == 0 || row.AverageR is null && row.NetProfit == 0 ? "—" : $"%{row.WinRate:0}",
                row.AverageR?.ToString("+0.00;-0.00;0.00") ?? "—", row.NetProfit == 0 && row.AverageR is null ? "—" : Plain(row.NetProfit, signed: true) },
            Colors: new Color?[] { null, null, null, ColorFor(row.AverageR), ColorFor(row.NetProfit) })).ToList();
        if (rows.Count > maxRows)
            lines.Add((new[] { $"+{rows.Count - maxRows} diğer", "", "", "", "" }, new Color?[5]));
        if (lines.Count == 0)
            lines.Add((new[] { "Veri yok", "", "", "", "" }, new Color?[5]));

        return Table(band, x, y, width,
            [title, "İşlem", "Win", "Ort. R", "Net"],
            [2.4f, 0.8f, 1f, 1f, 1.6f],
            [TextAlignment.MiddleLeft, TextAlignment.MiddleRight, TextAlignment.MiddleRight, TextAlignment.MiddleRight, TextAlignment.MiddleRight],
            lines);
    }

    // Başlık satırı + veri satırlarından oluşan tablo; tablonun bittiği y konumunu döndürür.
    private static float Table(Band band, float x, float y, float width, string[] headers, float[] weights, TextAlignment[] align,
        List<(string[] Cells, Color?[] Colors)> rows)
    {
        const float rowHeight = 22;
        var table = new XRTable
        {
            LocationF = new PointF(x, y),
            SizeF = new SizeF(width, rowHeight * (rows.Count + 1)),
            Borders = BorderSide.None
        };
        table.BeginInit();
        table.Rows.Add(TableRow(rowHeight, headers, weights, align, header: true));
        foreach ((string[] cells, Color?[] colors) in rows)
        {
            XRTableRow tr = TableRow(rowHeight, cells, weights, align, header: false);
            for (int i = 0; i < colors.Length && i < tr.Cells.Count; i++)
                if (colors[i] is Color color)
                    tr.Cells[i].ForeColor = color;
            table.Rows.Add(tr);
        }
        table.EndInit();
        band.Controls.Add(table);
        return y + rowHeight * (rows.Count + 1);
    }

    // İşlem listesi: başlık satırı her sayfada tekrar eder, satırlar sırayla zebra renkli.
    private (GroupHeaderBand Header, DetailBand Detail) BuildTradeList()
    {
        const float rowHeight = 20;
        float[] weights = [1.25f, 1.15f, 0.7f, 1.5f, 1.1f, 1.15f, 1.15f, 0.75f, 1.35f, 1.6f];
        string[] titles = ["Açılış", "Sembol", "Yön", "Setup", "Seans", "Giriş", "Çıkış", "R", "Net", "Etiketler"];
        TextAlignment[] align =
        [
            TextAlignment.MiddleLeft, TextAlignment.MiddleLeft, TextAlignment.MiddleLeft, TextAlignment.MiddleLeft, TextAlignment.MiddleLeft,
            TextAlignment.MiddleRight, TextAlignment.MiddleRight, TextAlignment.MiddleRight, TextAlignment.MiddleRight, TextAlignment.MiddleLeft
        ];

        var header = new GroupHeaderBand { PageBreak = PageBreak.BeforeBand, RepeatEveryPage = true, HeightF = 56 };
        float y = SectionTitle(header, 0, "İşlemler", $"Bu ay kapanan {_data.Trades.Count} işlem, kapanış sırasıyla");
        var headerTable = new XRTable { LocationF = new PointF(0, y), SizeF = new SizeF(ContentWidth, rowHeight), Borders = BorderSide.None };
        headerTable.BeginInit();
        headerTable.Rows.Add(TableRow(rowHeight, titles, weights, align, header: true));
        headerTable.EndInit();
        header.Controls.Add(headerTable);
        header.HeightF = y + rowHeight;

        var detail = new DetailBand { HeightF = rowHeight };
        var table = new XRTable { LocationF = new PointF(0, 0), SizeF = new SizeF(ContentWidth, rowHeight), Borders = BorderSide.None };
        table.BeginInit();
        XRTableRow row = TableRow(rowHeight, new string[titles.Length], weights, align, header: false);
        table.Rows.Add(row);
        table.EndInit();
        detail.Controls.Add(table);

        // Hücre metinleri her satır yazdırılmadan hemen önce o satırın verisiyle doldurulur.
        int index = 0;
        detail.BeforePrint += (_, _) =>
        {
            if (GetCurrentRow() is not ReportTradeRow t) return;
            string[] values =
            [
                t.OpenTime.ToString("dd.MM HH:mm"),
                t.Symbol,
                t.Direction,
                t.Setup,
                t.Session,
                Price(t.EntryPrice),
                t.ExitPrice is decimal exit ? Price(exit) : "—",
                t.RMultiple?.ToString("+0.00;-0.00;0.00") ?? "—",
                t.NetPnL is decimal net ? Plain(net, signed: true) : "—",
                t.Tags
            ];
            for (int i = 0; i < values.Length; i++)
                row.Cells[i].Text = values[i];

            row.Cells[7].ForeColor = ColorFor(t.RMultiple);
            row.Cells[8].ForeColor = ColorFor(t.NetPnL);
            table.BackColor = index++ % 2 == 0 ? Color.White : Zebra;
        };

        return (header, detail);
    }

    private static void BuildFooter(Band band, string period)
    {
        band.Controls.Add(Label($"EdgeBook  ·  {period} raporu", 0, 14, 400, 16, F(7.5f), Faint));
        band.Controls.Add(new XRPageInfo
        {
            LocationF = new PointF(ContentWidth - 200, 14),
            SizeF = new SizeF(200, 16),
            PageInfo = PageInfo.NumberOfTotal,
            TextFormatString = "Sayfa {0} / {1}",
            Font = F(7.5f),
            ForeColor = Faint,
            TextAlignment = TextAlignment.MiddleRight,
            Borders = BorderSide.None
        });
    }

    // ---------------- Yardımcılar ----------------

    private static DXFont F(float size, bool bold = false)
        => new("Segoe UI", size, bold ? DXFontStyle.Bold : DXFontStyle.Regular);

    private static XRLabel Label(string text, float x, float y, float w, float h, DXFont font, Color color,
        TextAlignment align = TextAlignment.MiddleLeft)
        => new()
        {
            Text = text,
            LocationF = new PointF(x, y),
            SizeF = new SizeF(w, h),
            Font = font,
            ForeColor = color,
            TextAlignment = align,
            Padding = new PaddingInfo(0, 0, 0, 0),
            Borders = BorderSide.None,
            WordWrap = false,
            CanGrow = false
        };

    // Bölüm başlığı + açıklama; başlığın altındaki ilk y konumunu döndürür.
    private static float SectionTitle(Band band, float y, string title, string hint)
    {
        band.Controls.Add(Label(title, 0, y, 300, 22, F(11, bold: true), Ink));
        band.Controls.Add(Label(hint, 300, y + 4, ContentWidth - 300, 16, F(7.5f), Faint, TextAlignment.MiddleRight));
        return y + 26;
    }

    private static XRTableRow TableRow(float height, string?[] texts, float[] weights, TextAlignment[] align, bool header)
    {
        var row = new XRTableRow { HeightF = height };
        for (int i = 0; i < texts.Length; i++)
        {
            row.Cells.Add(new XRTableCell
            {
                Text = texts[i] ?? "",
                Weight = weights[i],
                Font = header ? F(7.5f, bold: true) : F(8.5f),
                ForeColor = header ? Muted : Ink,
                BackColor = header ? HeaderBack : Color.Transparent,
                TextAlignment = align[i],
                Padding = new PaddingInfo(6, 6, 0, 0),
                Borders = BorderSide.Bottom,
                BorderColor = Line,
                BorderWidth = 1,
                WordWrap = false
            });
        }
        return row;
    }

    private static XRChart Chart(float y, float height)
    {
        var chart = new XRChart
        {
            LocationF = new PointF(0, y),
            SizeF = new SizeF(ContentWidth, height),
            BackColor = Color.White,
            Borders = BorderSide.None
        };
        chart.Legend.Visibility = DefaultBoolean.False;
        return chart;
    }

    private static void StyleDiagram(Charts.XYDiagram diagram)
    {
        diagram.DefaultPane.BackColor = Color.White;
        diagram.DefaultPane.BorderVisible = false;
        diagram.AxisX.Color = Line;
        diagram.AxisY.Color = Color.Transparent;
        diagram.AxisX.Label.TextColor = Muted;
        diagram.AxisY.Label.TextColor = Muted;
        diagram.AxisX.GridLines.Visible = false;
        diagram.AxisY.GridLines.Visible = true;
        diagram.AxisY.GridLines.Color = Line;
    }

    private static Color ColorFor(decimal? value) => value > 0 ? Profit : value < 0 ? Loss : Ink;

    private string Plain(decimal value, bool signed = false)
    {
        string number = signed
            ? MonthlyReportService.Money(value, "")
            : value.ToString("#,##0.00", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        return string.IsNullOrEmpty(Currency) || signed ? number : $"{number} {Currency}";
    }

    // Fiyatın büyüklüğüne göre okunur basamak sayısı (önizlemedeki ile aynı kural).
    private static string Price(decimal price)
    {
        decimal abs = Math.Abs(price);
        string format = abs >= 1000 ? "#,##0.00" : abs >= 10 ? "0.000" : abs >= 1 ? "0.00000" : "0.000000";
        return price.ToString(format, System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
    }
}

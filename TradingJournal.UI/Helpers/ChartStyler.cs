using DevExpress.Utils;
using DevExpress.XtraCharts;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Helpers;

// ChartControl'leri uygulamanın koyu temasına uygun hale getirir.
public static class ChartStyler
{
    public static ChartControl Create()
    {
        var chart = new ChartControl
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Card
        };
        chart.BorderOptions.Visibility = DefaultBoolean.False;
        chart.Legend.Visibility = DefaultBoolean.False;
        chart.CrosshairEnabled = DefaultBoolean.True;
        chart.EmptyChartText.Text = "Henüz kapanmış işlem yok";
        chart.EmptyChartText.TextColor = Theme.TextMuted;
        return chart;
    }

    // Diyagram ancak seri eklendikten sonra oluşur; bu yüzden her yeniden çizimde çağrılır.
    public static void StyleDiagram(ChartControl chart, string? yPattern = null)
    {
        if (chart.Diagram is not XYDiagram diagram)
            return;

        diagram.DefaultPane.BackColor = Theme.Card;
        diagram.DefaultPane.BorderVisible = false;

        foreach (Axis axis in new Axis[] { diagram.AxisX, diagram.AxisY })
        {
            axis.Color = Theme.Border;
            axis.Label.TextColor = Theme.TextMuted;
            axis.GridLines.Color = Theme.Border;
        }

        diagram.AxisX.GridLines.Visible = false;
        diagram.AxisY.GridLines.Visible = true;
        diagram.AxisY.WholeRange.AlwaysShowZeroLevel = false;
        if (yPattern is not null)
            diagram.AxisY.Label.TextPattern = yPattern;
    }

    // Pozitif değerler yeşil, negatifler kırmızı çubuk.
    public static Series SignedBars(string name, IEnumerable<(string Label, double Value)> points, bool colorBySign = true)
    {
        var series = new Series(name, ViewType.Bar) { ArgumentScaleType = ScaleType.Qualitative };
        foreach ((string label, double value) in points)
        {
            var point = new SeriesPoint(label, value);
            point.Color = colorBySign ? (value >= 0 ? Theme.Profit : Theme.Loss) : Theme.Accent;
            series.Points.Add(point);
        }
        if (series.View is BarSeriesView bar)
        {
            bar.BarWidth = 0.6;
            bar.FillStyle.FillMode = FillMode.Solid;
            bar.Border.Visibility = DefaultBoolean.False;
        }
        return series;
    }
}

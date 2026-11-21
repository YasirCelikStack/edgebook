using System.ComponentModel;
using DevExpress.Utils;
using DevExpress.XtraCharts;
using TradingJournal.Core.Entities;
using TradingJournal.Services.Risk;
using TradingJournal.Services.Statistics;
using TradingJournal.UI.Controls;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Pages;

// Genel bakış: KPI kartları, equity curve, R dağılımı, seans performansı ve risk kuralları.
[DesignerCategory("Code")]
public class DashboardPage : PageBase
{
    private readonly IStatisticsService _statistics = new StatisticsService();
    private readonly RiskService _risk = new();

    private readonly StatCard _netCard = new("Net kâr", "\uE8C7");
    private readonly StatCard _winRateCard = new("Win rate", "\uE9D2");
    private readonly StatCard _pfCard = new("Profit factor", "\uE8EF");
    private readonly StatCard _avgRCard = new("Ortalama R", "\uE9D9");
    private readonly StatCard _expectancyCard = new("Beklenti", "\uE8B5");
    private readonly StatCard _drawdownCard = new("Max drawdown", "\uE74B");

    private readonly ChartControl _equityChart = ChartStyler.Create();
    private readonly ChartControl _rChart = ChartStyler.Create();
    private readonly ChartControl _sessionChart = ChartStyler.Create();
    private readonly CardPanel _equityCard = new("Equity curve");
    private readonly RiskMeterList _riskList = new() { Dock = DockStyle.Fill };

    public DashboardPage(AppState state) : base(state)
    {
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, BackColor = Theme.Background, Margin = Padding.Empty };
        StatCard[] all = [_netCard, _winRateCard, _pfCard, _avgRCard, _expectancyCard, _drawdownCard];
        for (int i = 0; i < all.Length; i++)
        {
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / all.Length));
            all[i].Dock = DockStyle.Fill;
            cards.Controls.Add(all[i], i, 0);
        }

        _equityCard.Dock = DockStyle.Fill;
        _equityCard.Controls.Add(_equityChart);

        var rCard = new CardPanel("R dağılımı") { Dock = DockStyle.Fill, Hint = "işlem sayısı" };
        rCard.Controls.Add(_rChart);
        var sessionCard = new CardPanel("Seans performansı") { Dock = DockStyle.Fill, Hint = "net kâr" };
        sessionCard.Controls.Add(_sessionChart);
        var riskCard = new CardPanel("Risk kuralları") { Dock = DockStyle.Fill };
        riskCard.Controls.Add(_riskList);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Theme.Background, Margin = Padding.Empty };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        bottom.Controls.Add(rCard, 0, 0);
        bottom.Controls.Add(sessionCard, 1, 0);
        bottom.Controls.Add(riskCard, 2, 0);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Theme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.Controls.Add(cards, 0, 0);
        root.Controls.Add(_equityCard, 0, 1);
        root.Controls.Add(bottom, 0, 2);
        Controls.Add(root);
    }

    public override string Title => "Panel";
    public override string Subtitle => "Performansına genel bakış";

    public override async Task RefreshAsync()
    {
        List<Trade> trades = await State.LoadTradesAsync();
        List<Account> accounts = await State.LoadAccountsAsync();
        List<Account> scope = State.AccountId is int id ? accounts.Where(a => a.Id == id).ToList() : accounts;

        TradeStatistics stats = _statistics.Calculate(trades);
        UpdateCards(stats, trades.Count(t => !t.IsClosed));
        UpdateEquity(trades, scope.Sum(a => a.InitialBalance));
        UpdateRDistribution(trades);
        UpdateSessions(trades);
        UpdateRisk(scope, trades);
    }

    private void UpdateCards(TradeStatistics s, int openCount)
    {
        _netCard.SetValue(s.NetProfit.ToString("+#,0.00;-#,0.00;0.00"),
            $"{s.TotalTrades} kapanmış · {openCount} açık işlem", Theme.ForValue(s.NetProfit));
        _winRateCard.SetValue($"%{s.WinRate:0.#}", $"{s.Wins} kazanç · {s.Losses} kayıp",
            s.TotalTrades == 0 ? null : s.WinRate >= 50 ? Theme.Profit : Theme.Warning);
        _pfCard.SetValue(s.ProfitFactor?.ToString("0.00") ?? "-", "brüt kâr / brüt zarar",
            s.ProfitFactor is null ? null : s.ProfitFactor >= 1 ? Theme.Profit : Theme.Loss);
        _avgRCard.SetValue(s.AverageR is decimal r ? $"{r:+0.00;-0.00;0.00}R" : "-", "işlem başına risk katı",
            Theme.ForValue(s.AverageR));
        _expectancyCard.SetValue(s.Expectancy.ToString("+#,0.00;-#,0.00;0.00"), "işlem başına ortalama",
            Theme.ForValue(s.Expectancy));
        _drawdownCard.SetValue(s.MaxDrawdown.ToString("#,0.00"), $"en büyük kayıp {s.LargestLoss:#,0.00}",
            s.MaxDrawdown > 0 ? Theme.Loss : null);
    }

    private void UpdateEquity(List<Trade> trades, decimal initialBalance)
    {
        List<EquityPoint> curve = _statistics.BuildEquityCurve(initialBalance, trades);
        _equityCard.Hint = curve.Count == 0 ? null : $"başlangıç {initialBalance:#,0} → şimdi {curve[^1].Balance:#,0}";
        _equityCard.Invalidate();

        _equityChart.Series.Clear();
        if (curve.Count == 0) return;

        var series = new Series("Bakiye", ViewType.Area) { ArgumentScaleType = ScaleType.Numerical };
        series.Points.Add(new SeriesPoint(0, (double)initialBalance));
        for (int i = 0; i < curve.Count; i++)
            series.Points.Add(new SeriesPoint(i + 1, (double)curve[i].Balance));

        bool up = curve[^1].Balance >= initialBalance;
        if (series.View is AreaSeriesView area)
        {
            area.Color = up ? Theme.Profit : Theme.Loss;
            area.Transparency = 190;
            area.Border.Color = up ? Theme.Profit : Theme.Loss;
            area.Border.Thickness = 2;
            area.MarkerVisibility = DefaultBoolean.False;
        }
        _equityChart.Series.Add(series);
        ChartStyler.StyleDiagram(_equityChart, "{V:#,0}");
        if (_equityChart.Diagram is XYDiagram d)
        {
            d.AxisX.Title.Text = "İşlem sırası";
            d.AxisX.Title.TextColor = Theme.TextFaint;
            d.AxisX.Title.Visibility = DefaultBoolean.True;
        }
    }

    private void UpdateRDistribution(List<Trade> trades)
    {
        // R değerlerini kovalara ayır: kayıplar solda, kazançlar sağda.
        (string Label, Func<decimal, bool> Match)[] buckets =
        [
            ("≤ -2R", r => r <= -2),
            ("-2..-1", r => r > -2 && r <= -1),
            ("-1..0", r => r > -1 && r < 0),
            ("0..1", r => r >= 0 && r < 1),
            ("1..2", r => r >= 1 && r < 2),
            ("2..3", r => r >= 2 && r < 3),
            ("≥ 3R", r => r >= 3),
        ];
        List<decimal> rValues = trades.Where(t => t.RMultiple.HasValue).Select(t => t.RMultiple!.Value).ToList();

        _rChart.Series.Clear();
        if (rValues.Count == 0) return;

        var series = ChartStyler.SignedBars("R", buckets.Select(b => (b.Label, (double)rValues.Count(b.Match))), colorBySign: false);
        for (int i = 0; i < series.Points.Count; i++)
            series.Points[i].Color = i < 3 ? Theme.Loss : Theme.Profit;
        _rChart.Series.Add(series);
        ChartStyler.StyleDiagram(_rChart, "{V:0}");
    }

    private void UpdateSessions(List<Trade> trades)
    {
        var groups = _statistics.GroupBy(trades, t => SessionClassifier.GetSession(t.OpenTime))
            .OrderBy(g => g.Key)
            .Select(g => (g.Key.ToTurkish(), (double)g.Stats.NetProfit));

        _sessionChart.Series.Clear();
        Series series = ChartStyler.SignedBars("Seans", groups);
        if (series.Points.Count == 0) return;
        _sessionChart.Series.Add(series);
        ChartStyler.StyleDiagram(_sessionChart, "{V:#,0}");
    }

    private void UpdateRisk(List<Account> accounts, List<Trade> trades)
    {
        if (State.AccountId is null)
        {
            _riskList.SetMessage("Kuralları görmek için üstten bir hesap seç.");
            return;
        }

        Account? account = accounts.FirstOrDefault();
        if (account is null || !account.Rules.Any(r => r.IsActive))
        {
            _riskList.SetMessage("Bu hesapta risk kuralı yok.\nTanımlar → Hesaplar'dan ekleyebilirsin.");
            return;
        }

        _riskList.SetResults(_risk.CheckAccount(account, trades, DateTime.Now));
    }
}

// Risk kurallarının ne kadar dolduğunu ilerleme çubuklarıyla gösterir.
[DesignerCategory("Code")]
public class RiskMeterList : Control
{
    private List<RiskCheckResult> _results = new();
    private string? _message;

    public RiskMeterList()
    {
        DoubleBuffered = true;
        BackColor = Theme.Card;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public void SetMessage(string message)
    {
        _message = message;
        _results = new();
        Invalidate();
    }

    public void SetResults(List<RiskCheckResult> results)
    {
        _message = null;
        _results = results;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        if (_message is not null)
        {
            TextRenderer.DrawText(g, _message, Theme.Body, ClientRectangle, Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        int y = 4;
        foreach (RiskCheckResult result in _results)
        {
            Color color = result.IsViolated ? Theme.Loss : result.IsWarning ? Theme.Warning : Theme.Profit;
            TextRenderer.DrawText(g, result.RuleName, Theme.BodyBold, new Point(0, y), Theme.Text);
            TextRenderer.DrawText(g, $"%{result.UsagePercent:0.#}", Theme.BodyBold,
                new Rectangle(0, y, Width, 20), color, TextFormatFlags.Right);

            var track = new RectangleF(0, y + 26, Width - 1, 10);
            Theme.FillRounded(g, Theme.Background, track, 5);
            float fill = (float)Math.Clamp((double)result.UsagePercent / 100.0, 0, 1) * track.Width;
            if (fill > 0)
                Theme.FillRounded(g, color, new RectangleF(track.X, track.Y, Math.Max(fill, 10), track.Height), 5);

            TextRenderer.DrawText(g, $"{result.CurrentLoss:#,0.00} / {result.LimitAmount:#,0.00}", Theme.Small,
                new Point(0, y + 42), Theme.TextMuted);
            if (result.IsViolated)
                TextRenderer.DrawText(g, "İHLAL", Theme.SmallBold, new Rectangle(0, y + 42, Width, 18), Theme.Loss, TextFormatFlags.Right);

            y += 76;
        }
    }
}

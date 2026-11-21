using System.ComponentModel;
using System.Globalization;
using TradingJournal.Core.Entities;
using TradingJournal.UI.Controls;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Pages;

// Aylık PnL takvimi. İşlemler kapanış gününe göre toplanır.
[DesignerCategory("Code")]
public class CalendarPage : PageBase
{
    private readonly PnlCalendar _calendar = new() { Dock = DockStyle.Fill };
    private readonly Label _monthLabel = new() { AutoSize = true, Font = Theme.Title, ForeColor = Theme.Text, Margin = new Padding(12, 4, 12, 0) };
    private readonly Label _summaryLabel = new() { AutoSize = true, Font = Theme.Body, ForeColor = Theme.TextMuted, Margin = new Padding(16, 12, 0, 0) };
    private List<Trade> _trades = new();

    // Bir güne tıklanınca ana pencere İşlemler sayfasını o güne filtreli açar.
    public event EventHandler<DateTime>? DaySelected;

    public CalendarPage(AppState state) : base(state)
    {
        var prev = new FlatButton("", Theme.Glyph.Back) { Width = 40 };
        var next = new FlatButton("", Theme.Glyph.Forward) { Width = 40 };
        var today = new FlatButton("Bugün");
        prev.Click += (_, _) => ChangeMonth(-1);
        next.Click += (_, _) => ChangeMonth(1);
        today.Click += (_, _) => { _calendar.Month = DateTime.Today; UpdateCalendar(); };

        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.Background, WrapContents = false };
        header.Controls.AddRange([prev, _monthLabel, next, today, _summaryLabel]);

        var card = new CardPanel { Dock = DockStyle.Fill };
        card.Controls.Add(_calendar);

        _calendar.DayClicked += (_, day) => DaySelected?.Invoke(this, day);

        Controls.Add(card);
        Controls.Add(header);
    }

    public override string Title => "Takvim";
    public override string Subtitle => "Günlük sonuçların. Bir güne tıkla, o günün işlemlerini gör.";

    public override async Task RefreshAsync()
    {
        _trades = await State.LoadTradesAsync();
        UpdateCalendar();
    }

    private void ChangeMonth(int delta)
    {
        _calendar.Month = _calendar.Month.AddMonths(delta);
        UpdateCalendar();
    }

    private void UpdateCalendar()
    {
        var days = _trades
            .Where(t => t.IsClosed)
            .GroupBy(t => t.CloseTime!.Value.Date)
            .ToDictionary(g => g.Key, g => new PnlCalendar.DaySummary(g.Sum(t => t.NetPnL!.Value), g.Count()));
        _calendar.SetData(days);

        DateTime month = _calendar.Month;
        _monthLabel.Text = month.ToString("MMMM yyyy", new CultureInfo("tr-TR"));

        var monthDays = days.Where(d => d.Key.Year == month.Year && d.Key.Month == month.Month).Select(d => d.Value).ToList();
        decimal net = monthDays.Sum(d => d.NetPnL);
        int green = monthDays.Count(d => d.NetPnL > 0);
        int red = monthDays.Count(d => d.NetPnL < 0);
        _summaryLabel.Text = monthDays.Count == 0
            ? "Bu ay kapanmış işlem yok"
            : $"Ay toplamı: {net:+#,0.00;-#,0.00;0}   ·   {green} yeşil gün, {red} kırmızı gün";
        _summaryLabel.ForeColor = monthDays.Count == 0 ? Theme.TextMuted : Theme.ForValue(net);
    }
}

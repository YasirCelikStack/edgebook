using System.ComponentModel;
using System.Globalization;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Controls;

// Aylık PnL takvimi. Her gün hücresinde o günün net sonucu ve işlem sayısı,
// en sağ sütunda haftanın toplamı görünür. Kâr yeşil, zarar kırmızı; tutar büyüdükçe renk koyulaşır.
// Tıklanan gün seçilir (günlük not için); çift tıklanan gün işlemler listesinde açılır.
// Notu olan günlerin sağ üst köşesinde küçük bir not işareti çizilir.
[DesignerCategory("Code")]
public class PnlCalendar : Control
{
    private static readonly string[] DayNames = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz", "Hafta"];
    private const int HeaderHeight = 32;

    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private Dictionary<DateTime, DaySummary> _days = new();
    private DateTime? _hoverDay;
    private DateTime? _selectedDay;
    private HashSet<DateTime> _noteDays = new();

    public record DaySummary(decimal NetPnL, int TradeCount);

    // Çift tıklama: o günün işlemlerini aç (sadece işlem olan günlerde).
    public event EventHandler<DateTime>? DayClicked;

    // Tek tıklama: günü seç (her gün seçilebilir, işlem olmasa da).
    public event EventHandler<DateTime>? DaySelected;

    public PnlCalendar()
    {
        DoubleBuffered = true;
        BackColor = Theme.Card;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime Month
    {
        get => _month;
        set { _month = new DateTime(value.Year, value.Month, 1); Invalidate(); }
    }

    public void SetData(Dictionary<DateTime, DaySummary> days)
    {
        _days = days;
        Invalidate();
    }

    public void SetNoteDays(IEnumerable<DateTime> days)
    {
        _noteDays = days.Select(d => d.Date).ToHashSet();
        Invalidate();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime? SelectedDay
    {
        get => _selectedDay;
        set { _selectedDay = value?.Date; Invalidate(); }
    }

    // Ayın ilk gününün bulunduğu haftanın pazartesisi.
    private DateTime FirstVisibleDay
    {
        get
        {
            int offset = ((int)_month.DayOfWeek + 6) % 7; // Pazartesi = 0
            return _month.AddDays(-offset);
        }
    }

    private int WeekCount
    {
        get
        {
            DateTime last = _month.AddMonths(1).AddDays(-1);
            return (int)Math.Ceiling((last - FirstVisibleDay).TotalDays / 7.0 + 0.0001);
        }
    }

    private RectangleF CellRect(int column, int week)
    {
        float cellWidth = (Width - 1) / 8f;
        float cellHeight = (Height - HeaderHeight - 1) / (float)Math.Max(WeekCount, 5);
        return new RectangleF(column * cellWidth, HeaderHeight + week * cellHeight, cellWidth, cellHeight);
    }

    private DateTime? DayAt(Point point)
    {
        for (int week = 0; week < WeekCount; week++)
            for (int col = 0; col < 7; col++)
                if (CellRect(col, week).Contains(point))
                    return FirstVisibleDay.AddDays(week * 7 + col);
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        DateTime? day = DayAt(e.Location);
        if (day != _hoverDay)
        {
            _hoverDay = day;
            Cursor = day is not null ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverDay = null;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left || DayAt(e.Location) is not DateTime day) return;
        SelectedDay = day;
        DaySelected?.Invoke(this, day);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left && DayAt(e.Location) is DateTime day && _days.ContainsKey(day))
            DayClicked?.Invoke(this, day);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Card);

        // En büyük günlük tutar renk yoğunluğunun ölçeğidir.
        decimal maxAbs = _days.Count == 0 ? 1 : Math.Max(1, _days.Values.Max(d => Math.Abs(d.NetPnL)));

        for (int col = 0; col < 8; col++)
        {
            RectangleF header = CellRect(col, 0);
            TextRenderer.DrawText(g, DayNames[col], Theme.SmallBold,
                new Rectangle((int)header.X, 0, (int)header.Width, HeaderHeight), Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        for (int week = 0; week < WeekCount; week++)
        {
            decimal weekTotal = 0;
            int weekTrades = 0;

            for (int col = 0; col < 7; col++)
            {
                DateTime day = FirstVisibleDay.AddDays(week * 7 + col);
                RectangleF cell = CellRect(col, week);
                RectangleF inner = RectangleF.Inflate(cell, -3, -3);
                bool inMonth = day.Month == _month.Month;

                _days.TryGetValue(day, out DaySummary? summary);
                Color fill = Theme.Background;
                if (summary is not null && inMonth)
                {
                    weekTotal += summary.NetPnL;
                    weekTrades += summary.TradeCount;
                    double strength = 0.15 + 0.45 * (double)(Math.Abs(summary.NetPnL) / maxAbs);
                    fill = Theme.Tint(Theme.ForValue(summary.NetPnL == 0 ? null : summary.NetPnL), strength, Theme.Background);
                }
                if (_hoverDay == day)
                    fill = summary is not null ? ControlPaint.Light(fill, 0.15f) : Theme.CardHover;

                Theme.FillRounded(g, fill, inner, 8);
                if (day == _selectedDay || day == DateTime.Today)
                {
                    using var path = Theme.RoundedRect(inner, 8);
                    using var pen = new Pen(day == _selectedDay ? Color.White : Theme.Accent, day == _selectedDay ? 2f : 1.5f);
                    g.DrawPath(pen, path);
                }

                // Notu olan gün: sağ üstte küçük not simgesi.
                if (_noteDays.Contains(day))
                {
                    TextRenderer.DrawText(g, "\uE70B", Theme.Icons,
                        new Rectangle((int)inner.Right - 26, (int)inner.Y + 4, 22, 20), inMonth ? Theme.Accent : Theme.TextFaint,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }

                Color dayColor = inMonth ? Theme.TextMuted : Theme.TextFaint;
                TextRenderer.DrawText(g, day.Day.ToString(), Theme.Small,
                    new Point((int)inner.X + 8, (int)inner.Y + 6), dayColor);

                if (summary is not null && inMonth)
                    DrawAmount(g, inner, summary.NetPnL, summary.TradeCount);
            }

            // Haftalık toplam sütunu
            RectangleF weekCell = RectangleF.Inflate(CellRect(7, week), -3, -3);
            Theme.FillRounded(g, Theme.CardHover, weekCell, 8);
            TextRenderer.DrawText(g, $"{week + 1}. hafta", Theme.Small,
                new Point((int)weekCell.X + 8, (int)weekCell.Y + 6), Theme.TextFaint);
            if (weekTrades > 0)
                DrawAmount(g, weekCell, weekTotal, weekTrades);
        }
    }

    private static void DrawAmount(Graphics g, RectangleF cell, decimal pnl, int trades)
    {
        string amount = pnl.ToString("+#,0.##;-#,0.##;0", CultureInfo.CurrentCulture);
        var box = Rectangle.Round(cell);
        TextRenderer.DrawText(g, amount, Theme.BodyBold, box, pnl == 0 ? Theme.Text : Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, $"{trades} işlem", Theme.Small,
            new Rectangle(box.X, box.Bottom - 22, box.Width, 18), Theme.Text,
            TextFormatFlags.HorizontalCenter);
    }
}

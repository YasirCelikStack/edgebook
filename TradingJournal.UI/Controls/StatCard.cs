using System.ComponentModel;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Controls;

// Panel sayfasındaki KPI kartı: küçük başlık, büyük değer, altında açıklama.
[DesignerCategory("Code")]
public class StatCard : Control
{
    private string _value = "-";
    private string _caption = string.Empty;
    private Color _valueColor = Theme.Text;

    public StatCard(string title, string glyph)
    {
        Title = title;
        Glyph = glyph;
        DoubleBuffered = true;
        BackColor = Theme.Background;
        Margin = new Padding(8);
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
    }

    public string Title { get; }
    public string Glyph { get; }

    public void SetValue(string value, string caption = "", Color? valueColor = null)
    {
        _value = value;
        _caption = caption;
        _valueColor = valueColor ?? Theme.Text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.FillRounded(g, Theme.Card, new RectangleF(0, 0, Width - 1, Height - 1), 12);

        // Sol üstte renkli ikon kutucuğu
        var iconBox = new RectangleF(16, 16, 34, 34);
        Theme.FillRounded(g, Theme.Tint(_valueColor == Theme.Text ? Theme.Accent : _valueColor, 0.18), iconBox, 9);
        TextRenderer.DrawText(g, Glyph, Theme.Icons, Rectangle.Round(iconBox),
            _valueColor == Theme.Text ? Theme.Accent : _valueColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        TextRenderer.DrawText(g, Title, Theme.Body, new Point(60, 24), Theme.TextMuted);
        TextRenderer.DrawText(g, _value, Theme.BigNumber, new Point(14, 58), _valueColor);
        TextRenderer.DrawText(g, _caption, Theme.Small, new Point(16, Height - 28), Theme.TextFaint);
    }
}

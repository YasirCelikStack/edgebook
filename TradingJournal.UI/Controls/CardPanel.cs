using System.ComponentModel;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Controls;

// Yuvarlak köşeli, başlıklı içerik kartı. İçine grafik, liste vb. konur.
[DesignerCategory("Code")]
public class CardPanel : Panel
{
    private string _title = string.Empty;

    public CardPanel(string title = "")
    {
        _title = title;
        DoubleBuffered = true;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Margin = new Padding(8);
        UpdatePadding();
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public string Title
    {
        get => _title;
        set { _title = value; UpdatePadding(); Invalidate(); }
    }

    // Başlığın sağında gösterilen küçük açıklama (örn. "son 30 işlem").
    public string? Hint { get; set; }

    private void UpdatePadding()
        => Padding = string.IsNullOrEmpty(_title) ? new Padding(14) : new Padding(14, 44, 14, 12);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Theme.FillRounded(e.Graphics, Theme.Card, new RectangleF(0, 0, Width - 1, Height - 1), 12);

        if (!string.IsNullOrEmpty(_title))
        {
            TextRenderer.DrawText(e.Graphics, _title, Theme.Heading, new Point(14, 14), Theme.Text);
            if (!string.IsNullOrEmpty(Hint))
            {
                Size size = TextRenderer.MeasureText(Hint, Theme.Small);
                TextRenderer.DrawText(e.Graphics, Hint, Theme.Small, new Point(Width - size.Width - 14, 17), Theme.TextMuted);
            }
        }
    }
}

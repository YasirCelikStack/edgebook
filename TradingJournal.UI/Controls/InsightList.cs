using System.ComponentModel;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Controls;

// Alt alta satırlar: solda başlık + küçük açıklama, sağda renkli değer.
// Analiz sayfasındaki "davranış" ve "en iyi / en kötü kurulumlar" listeleri için.
[DesignerCategory("Code")]
public class InsightList : Control
{
    public record Line(string Title, string Value, string? Detail = null, Color? ValueColor = null);

    private const int LineHeight = 46;
    private List<Line> _lines = new();
    private string? _emptyText;

    public InsightList()
    {
        DoubleBuffered = true;
        BackColor = Theme.Card;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public int PreferredHeight => Math.Max(1, _lines.Count) * LineHeight;

    public void SetLines(List<Line> lines, string emptyText = "Yeterli veri yok")
    {
        _lines = lines;
        _emptyText = emptyText;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        if (_lines.Count == 0)
        {
            TextRenderer.DrawText(g, _emptyText ?? string.Empty, Theme.Small, ClientRectangle, Theme.TextFaint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            return;
        }

        int y = 0;
        using var pen = new Pen(Theme.Border);
        foreach (Line line in _lines)
        {
            Size valueSize = TextRenderer.MeasureText(line.Value, Theme.BodyBold);
            int titleWidth = Width - valueSize.Width - 12;

            TextRenderer.DrawText(g, line.Title, Theme.Body, new Rectangle(0, y + 4, titleWidth, 20), Theme.Text,
                TextFormatFlags.EndEllipsis);
            if (line.Detail is not null)
                TextRenderer.DrawText(g, line.Detail, Theme.Small, new Rectangle(0, y + 24, titleWidth, 18), Theme.TextMuted,
                    TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, line.Value, Theme.BodyBold, new Rectangle(0, y + 4, Width, 20), line.ValueColor ?? Theme.Text,
                TextFormatFlags.Right);

            y += LineHeight;
            if (y < Height)
                g.DrawLine(pen, 0, y - 2, Width, y - 2);
        }
    }
}

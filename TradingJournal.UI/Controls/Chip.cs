using System.ComponentModel;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Controls;

// Küçük renkli rozet: etiketler, Long/Short, Açık/Kapalı durumları için.
[DesignerCategory("Code")]
public class Chip : Control
{
    public Chip(string text, Color color)
    {
        Text = text;
        ChipColor = color;
        Font = Theme.SmallBold;
        DoubleBuffered = true;
        Margin = new Padding(0, 0, 6, 6);
        Size = new Size(TextRenderer.MeasureText(text, Font).Width + 20, 24);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    public Color ChipColor { get; }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme.FillRounded(e.Graphics, Theme.Tint(ChipColor, 0.22), new RectangleF(0, 0, Width - 1, Height - 1), 11);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ControlPaint.Light(ChipColor, 0.4f),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

using System.ComponentModel;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Controls;

// Yuvarlak köşeli, ikonlu düz buton. Primary = dolu mavi, değilse sadece kenarlıklı.
[DesignerCategory("Code")]
public class FlatButton : Control
{
    private bool _hover;
    private bool _pressed;

    public FlatButton(string text, string? glyph = null, bool primary = false)
    {
        Text = text;
        Glyph = glyph;
        Primary = primary;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        Height = 36;
        Font = Theme.BodyBold;
        Margin = new Padding(4);
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Width = MeasureWidth();
    }

    public string? Glyph { get; set; }
    public bool Primary { get; set; }

    // Sil gibi tehlikeli işlemler için kırmızı vurgulu.
    public bool Danger { get; set; }

    private int MeasureWidth()
    {
        int textWidth = TextRenderer.MeasureText(Text, Font).Width;
        return textWidth + (Glyph is null ? 28 : 52);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Width = MeasureWidth();
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Color accent = Danger ? Theme.Loss : Theme.Accent;

        Color fill = Primary
            ? (_pressed ? ControlPaint.Dark(accent, 0.1f) : _hover ? ControlPaint.Light(accent, 0.15f) : accent)
            : (_hover ? Theme.CardHover : Theme.Card);
        Color fore = Primary ? Color.White : (_hover && Danger ? Theme.Loss : Theme.Text);
        if (!Enabled)
        {
            fill = Theme.Card;
            fore = Theme.TextFaint;
        }

        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Theme.FillRounded(g, fill, rect, 8);
        if (!Primary)
        {
            using var path = Theme.RoundedRect(rect, 8);
            using var pen = new Pen(Theme.Border);
            g.DrawPath(pen, path);
        }

        int x = 14;
        if (Glyph is not null)
        {
            TextRenderer.DrawText(g, Glyph, Theme.Icons, new Rectangle(x, 0, 22, Height), fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            x += 26;
        }
        TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, Width - x - 10, Height), fore,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }
}

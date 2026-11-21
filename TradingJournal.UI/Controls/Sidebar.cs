using System.ComponentModel;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Controls;

// Soldaki menü: logo + sayfa butonları. Bir butona basılınca ItemSelected olayı tetiklenir.
[DesignerCategory("Code")]
public class Sidebar : Panel
{
    private readonly List<NavItem> _items = new();
    private int _nextTop = 96;

    public event EventHandler<string>? ItemSelected;

    // Alttaki sürüm yazısına tıklanınca (güncellemeleri denetlemek için).
    public event EventHandler? VersionClicked;

    // Sol altta gösterilen sürüm, örn. "v1.0.0".
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string VersionText { get; set; } = string.Empty;

    private Rectangle VersionArea => new(0, Height - 56, Width, 56);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = VersionArea.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (VersionArea.Contains(e.Location))
            VersionClicked?.Invoke(this, EventArgs.Empty);
    }

    public Sidebar()
    {
        Dock = DockStyle.Left;
        Width = 224;
        BackColor = Theme.Sidebar;
        DoubleBuffered = true;
    }

    public void AddItem(string key, string text, string glyph)
    {
        var item = new NavItem(key, text, glyph)
        {
            Location = new Point(12, _nextTop),
            Size = new Size(Width - 24, 42)
        };
        item.Click += (_, _) => Select(key);
        _items.Add(item);
        Controls.Add(item);
        _nextTop += 48;
    }

    public void Select(string key)
    {
        foreach (NavItem item in _items)
            item.Selected = item.Key == key;
        ItemSelected?.Invoke(this, key);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;

        // Logo (Assets/logo-96.png, exe'ye gömülü) + uygulama adı
        Image? logo = TradingJournal.UI.Helpers.AppIcon.Logo;
        if (logo is not null)
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.DrawImage(logo, new Rectangle(20, 26, 40, 40));
        }
        else
        {
            // Logo yüklenemezse eski çizim: mavi kare içinde "E"
            Theme.FillRounded(g, Theme.Accent, new RectangleF(22, 28, 36, 36), 10);
            using var logoFont = new Font(Theme.FontName, 15f, FontStyle.Bold);
            TextRenderer.DrawText(g, "E", logoFont, new Rectangle(22, 28, 36, 36), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        using (var nameFont = new Font(Theme.FontName, 14f, FontStyle.Bold))
            TextRenderer.DrawText(g, "EdgeBook", nameFont, new Point(68, 26), Theme.Text);
        TextRenderer.DrawText(g, "Trading Journal", Theme.Small, new Point(70, 50), Theme.TextMuted);

        // Sağ kenarda ince ayırıcı çizgi
        using var pen = new Pen(Theme.Border);
        g.DrawLine(pen, Width - 1, 0, Width - 1, Height);

        TextRenderer.DrawText(g, $"EdgeBook {VersionText}", Theme.Small,
            new Rectangle(0, Height - 50, Width, 18), Theme.TextMuted, TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(g, "Güncellemeleri denetle", Theme.Small,
            new Rectangle(0, Height - 32, Width, 18), Theme.TextFaint, TextFormatFlags.HorizontalCenter);
    }

    [DesignerCategory("Code")]
    private class NavItem : Control
    {
        private bool _hover;
        private bool _selected;

        public NavItem(string key, string text, string glyph)
        {
            Key = key;
            Text = text;
            Glyph = glyph;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            BackColor = Theme.Sidebar;
        }

        public string Key { get; }
        public string Glyph { get; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Selected
        {
            get => _selected;
            set { _selected = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (_selected)
            {
                Theme.FillRounded(g, Theme.Tint(Theme.Accent, 0.18, Theme.Sidebar), new RectangleF(0, 0, Width - 1, Height - 1), 9);
                Theme.FillRounded(g, Theme.Accent, new RectangleF(0, 10, 4, Height - 20), 2);
            }
            else if (_hover)
            {
                Theme.FillRounded(g, Theme.Card, new RectangleF(0, 0, Width - 1, Height - 1), 9);
            }

            Color fore = _selected ? Theme.Text : Theme.TextMuted;
            TextRenderer.DrawText(g, Glyph, Theme.Icons, new Rectangle(14, 0, 24, Height),
                _selected ? Theme.Accent : fore, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, Text, _selected ? Theme.BodyBold : Theme.Body, new Rectangle(48, 0, Width - 48, Height),
                fore, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }
}

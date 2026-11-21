using System.ComponentModel;
using System.Drawing.Drawing2D;
using TradingJournal.Core.Entities;
using TradingJournal.Services.Screenshots;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Controls;

// Bir işlemin ekran görüntülerini küçük resimler halinde gösterir.
// Son kutu "+ Ekle" kutusudur. Resme tıklayınca büyük açılır, sağ üstteki çarpı ile silinir.
[DesignerCategory("Code")]
public class ScreenshotStrip : FlowLayoutPanel
{
    public const int TileWidth = 170;
    public const int TileHeight = 104;

    public event EventHandler? AddRequested;
    public event EventHandler<TradeScreenshot>? OpenRequested;
    public event EventHandler<TradeScreenshot>? DeleteRequested;

    public ScreenshotStrip()
    {
        FlowDirection = FlowDirection.LeftToRight;
        WrapContents = true;
        BackColor = Theme.Card;
        Padding = Padding.Empty;
        Margin = Padding.Empty;
    }

    public void SetScreenshots(IReadOnlyList<TradeScreenshot> screenshots)
    {
        SuspendLayout();
        foreach (Control old in Controls.Cast<Control>().ToList())
        {
            Controls.Remove(old);
            old.Dispose();
        }

        foreach (TradeScreenshot screenshot in screenshots)
        {
            var tile = new ThumbTile(screenshot);
            tile.Opened += (_, _) => OpenRequested?.Invoke(this, screenshot);
            tile.DeleteClicked += (_, _) => DeleteRequested?.Invoke(this, screenshot);
            Controls.Add(tile);
        }

        var addTile = new AddTile();
        addTile.Click += (_, _) => AddRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(addTile);
        ResumeLayout();
    }

    // İçerik yüksekliği: kart yüksekliğini buna göre ayarlarız.
    public int GetPreferredHeight(int availableWidth)
    {
        int perRow = Math.Max(1, availableWidth / (TileWidth + 8));
        int rows = (int)Math.Ceiling(Controls.Count / (double)perRow);
        return Math.Max(1, rows) * (TileHeight + 8);
    }

    // Resmi kilitlemeden küçültülmüş kopyasını oluşturur (orijinal dosya silinebilsin diye).
    private static Image? LoadThumbnail(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using Image original = Image.FromStream(stream);
            double scale = Math.Min(TileWidth * 2.0 / original.Width, TileHeight * 2.0 / original.Height);
            scale = Math.Min(scale, 1);
            var thumb = new Bitmap(Math.Max(1, (int)(original.Width * scale)), Math.Max(1, (int)(original.Height * scale)));
            using Graphics g = Graphics.FromImage(thumb);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(original, 0, 0, thumb.Width, thumb.Height);
            return thumb;
        }
        catch (Exception)
        {
            return null; // Dosya silinmiş ya da bozuk: kutuda uyarı gösterilir.
        }
    }

    [DesignerCategory("Code")]
    private class ThumbTile : Control
    {
        private readonly Image? _image;
        private bool _hover;

        public event EventHandler? Opened;
        public event EventHandler? DeleteClicked;

        public ThumbTile(TradeScreenshot screenshot)
        {
            _image = LoadThumbnail(ScreenshotService.GetFullPath(screenshot));
            Size = new Size(TileWidth, TileHeight);
            Margin = new Padding(0, 0, 8, 8);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            BackColor = Theme.Card;
        }

        private Rectangle DeleteBox => new(Width - 30, 6, 24, 24);

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (_hover && DeleteBox.Contains(e.Location))
                DeleteClicked?.Invoke(this, EventArgs.Empty);
            else
                Opened?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(0, 0, Width - 1, Height - 1);
            using GraphicsPath clip = Theme.RoundedRect(bounds, 8);

            Theme.FillRounded(g, Theme.Background, bounds, 8);
            if (_image is not null)
            {
                g.SetClip(clip);
                // Kutuyu dolduracak şekilde ortalayarak çiz (fazlası kırpılır).
                double scale = Math.Max(Width / (double)_image.Width, Height / (double)_image.Height);
                int w = (int)(_image.Width * scale), h = (int)(_image.Height * scale);
                g.DrawImage(_image, (Width - w) / 2, (Height - h) / 2, w, h);
                g.ResetClip();
            }
            else
            {
                TextRenderer.DrawText(g, "Dosya bulunamadı", Theme.Small, ClientRectangle, Theme.TextFaint,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            using var pen = new Pen(_hover ? Theme.Accent : Theme.Border, _hover ? 2 : 1);
            g.DrawPath(pen, clip);

            if (_hover)
            {
                Theme.FillRounded(g, Color.FromArgb(200, 0, 0, 0), DeleteBox, 6);
                TextRenderer.DrawText(g, Theme.Glyph.Delete, Theme.Small, DeleteBox, Theme.Loss,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _image?.Dispose();
            base.Dispose(disposing);
        }
    }

    [DesignerCategory("Code")]
    private class AddTile : Control
    {
        private bool _hover;

        public AddTile()
        {
            Size = new Size(TileWidth, TileHeight);
            Margin = new Padding(0, 0, 8, 8);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            BackColor = Theme.Card;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(1, 1, Width - 3, Height - 3);
            if (_hover)
                Theme.FillRounded(g, Theme.Tint(Theme.Accent, 0.1), bounds, 8);

            using (GraphicsPath path = Theme.RoundedRect(bounds, 8))
            using (var pen = new Pen(_hover ? Theme.Accent : Theme.TextFaint, 1.2f) { DashStyle = DashStyle.Dash })
                g.DrawPath(pen, path);

            Color color = _hover ? Theme.Accent : Theme.TextMuted;
            TextRenderer.DrawText(g, Theme.Glyph.Add, Theme.Icons, new Rectangle(0, 18, Width, 26), color,
                TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, "SS ekle", Theme.BodyBold, new Rectangle(0, 46, Width, 22), color,
                TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, "Ctrl+V · sürükle-bırak", Theme.Small, new Rectangle(0, 68, Width, 20), Theme.TextFaint,
                TextFormatFlags.HorizontalCenter);
        }
    }
}

using System.Drawing.Drawing2D;

namespace TradingJournal.UI.Theming;

// Uygulamanın renk ve yazı tipleri tek yerde: bir rengi değiştirmek tüm ekranlara yansır.
public static class Theme
{
    // Zemin katmanları (koyudan açığa)
    public static readonly Color Background = Color.FromArgb(17, 19, 24);
    public static readonly Color Sidebar = Color.FromArgb(12, 14, 18);
    public static readonly Color Card = Color.FromArgb(26, 29, 36);
    public static readonly Color CardHover = Color.FromArgb(33, 37, 46);
    public static readonly Color Border = Color.FromArgb(40, 44, 54);

    // Yazılar
    public static readonly Color Text = Color.FromArgb(230, 233, 239);
    public static readonly Color TextMuted = Color.FromArgb(139, 146, 160);
    public static readonly Color TextFaint = Color.FromArgb(90, 97, 110);

    // Vurgular
    public static readonly Color Accent = Color.FromArgb(56, 132, 255);
    public static readonly Color Profit = Color.FromArgb(38, 194, 129);
    public static readonly Color Loss = Color.FromArgb(240, 82, 82);
    public static readonly Color Warning = Color.FromArgb(245, 166, 35);

    public static Color ForValue(decimal? value)
        => value > 0 ? Profit : value < 0 ? Loss : TextMuted;

    // Rengi zemine doğru soldurur (0 = zemin, 1 = rengin kendisi). Takvim ve rozetlerde kullanılır.
    public static Color Tint(Color color, double strength, Color? background = null)
    {
        Color bg = background ?? Card;
        strength = Math.Clamp(strength, 0, 1);
        return Color.FromArgb(
            (int)(bg.R + (color.R - bg.R) * strength),
            (int)(bg.G + (color.G - bg.G) * strength),
            (int)(bg.B + (color.B - bg.B) * strength));
    }

    // Yazı tipleri
    public const string FontName = "Segoe UI";
    public static readonly Font Body = new(FontName, 9.75f);
    public static readonly Font BodyBold = new(FontName, 9.75f, FontStyle.Bold);
    public static readonly Font Small = new(FontName, 8.5f);
    public static readonly Font SmallBold = new(FontName, 8.5f, FontStyle.Bold);
    public static readonly Font Title = new(FontName, 16f, FontStyle.Bold);
    public static readonly Font Heading = new(FontName, 11f, FontStyle.Bold);
    public static readonly Font BigNumber = new(FontName, 20f, FontStyle.Bold);

    // Windows 10/11'de hazır gelen ikon yazı tipi: ikonlar resim dosyası gerektirmeden çizilir.
    public static readonly Font Icons = new("Segoe MDL2 Assets", 12f);
    public static readonly Font IconsLarge = new("Segoe MDL2 Assets", 20f);

    public static class Glyph
    {
        public const string Dashboard = "";
        public const string List = "";
        public const string Calendar = "";
        public const string Settings = "";
        public const string Add = "";
        public const string Edit = "";
        public const string Delete = "";
        public const string Check = "";
        public const string Picture = "";
        public const string Back = "";
        public const string Forward = "";
        public const string Save = "";
    }

    public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        if (d > bounds.Height) d = bounds.Height;
        if (d > bounds.Width) d = bounds.Width;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, Color color, RectangleF bounds, float radius)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = RoundedRect(bounds, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }
}

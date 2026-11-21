using System.Reflection;
using DevExpress.XtraEditors;

namespace TradingJournal.UI.Helpers;

// Uygulama simgesi ve logosu. Dosyalar (Assets/) exe'nin içine gömülü kaynak olarak eklenir,
// bu yüzden kurulumda ayrı dosya taşımaya gerek kalmaz.
internal static class AppIcon
{
    private static Icon? _icon;
    private static Image? _logo;

    // Pencere başlığı ve görev çubuğu simgesi (16–256 px boyutlarını içeren .ico).
    public static Icon? Icon => _icon ??= Load("EdgeBook.Icon", s => new Icon(s));

    // Kenar çubuğundaki logo (96x96 PNG, küçültülerek çizilir).
    public static Image? Logo => _logo ??= Load("EdgeBook.Logo", s =>
    {
        // Image.FromStream akışın açık kalmasını ister; kopyası alınınca akış kapatılabilir.
        using var img = Image.FromStream(s);
        return new Bitmap(img);
    });

    // Bir formun başlık çubuğuna simgeyi koyar.
    public static void Apply(XtraForm form)
    {
        if (Icon is null) return;
        form.Icon = Icon;
        form.IconOptions.Icon = Icon;
    }

    private static T? Load<T>(string resourceName, Func<Stream, T> create) where T : class
    {
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            return stream is null ? null : create(stream);
        }
        catch
        {
            // Simge yüklenemezse uygulama yine de açılsın (varsayılan simge kullanılır).
            return null;
        }
    }
}

using TradingJournal.Data;

namespace TradingJournal.UI.Helpers;

// Sürüm notları ve "Yenilikler" penceresinin ne zaman gösterileceği.
// Yeni sürüm yayınlamadan önce buraya o sürümün maddelerini ekle (en yeni en üstte).
public static class ReleaseNotes
{
    public record Item(string Glyph, string Title, string Description);
    public record Release(Version Version, IReadOnlyList<Item> Items);

    public static IReadOnlyList<Release> All { get; } =
    [
        new(new Version(1, 0, 4),
        [
            new("\uE73A", "Strateji kontrol listesi",
                "Tanımlar > Stratejiler'de her setup'a kurallarını yaz (her satır bir kural). İşlem girerken uyduğun kuralları işaretle; " +
                "Analiz sayfasındaki \"Kural uyumu\" kartı, kurallara uyduğun ve çiğnediğin işlemlerin sonucunu karşılaştırır."),
            new("\uE70B", "Günlük plan ve değerlendirme",
                "Takvimde bir güne tıkla: seans öncesi planını, gün sonu değerlendirmeni ve duygu durumunu yaz. " +
                "Notlu günler takvimde işaretlenir; çift tıklayınca o günün işlemleri açılır."),
            new("\uE99A", "Aylık rapora koç yorumu",
                "Aylık raporda \"Yapay zeka koç yorumu ekle\" seçeneği: Gemini ayın verilerine, kural uyumuna ve günlük notlarına bakıp " +
                "rapora kısa bir değerlendirme yazar. Rapora kural uyumu tablosu da eklendi.")
        ]),
        new(new Version(1, 0, 3),
        [
            new("", "Aylık PDF rapor",
                "Üst çubuktaki \"Aylık rapor\" düğmesiyle bir ay seç; özet, equity curve, günlük kâr/zarar, " +
                "setup/sembol/seans kırılımları ve ayın tüm işlemleriyle hazır raporu önizle ya da PDF olarak kaydet."),
            new("", "Yedek al / yedekten yükle",
                "Tanımlar sayfasından tüm verini (hesaplar, işlemler, ekran görüntüleri) tek bir .zip dosyasına yedekle. " +
                "Bilgisayar değişse de yedekten geri yükleyebilirsin."),
            new("", "Daha temiz ekranlar",
                "İnce, temaya uyan kaydırma çubukları; Chart Vault önizlemesinde fiyatlar artık okunur basamakla gösteriliyor.")
        ]),
        new(new Version(1, 0, 2),
        [
            new("", "Chart Vault'tan seçerek aktarma",
                "Önizlemede satırlara tıklayarak sadece istediğin işlemleri aktar. \"Görselliler\" düğmesi, " +
                "grafik görseli olan işlemleri tek tıkla seçer; görseller de işlemle birlikte iner.")
        ])
    ];

    private static string StatePath => Path.Combine(
        Path.GetDirectoryName(DbContextFactory.DefaultDatabasePath)!, "last-version.txt");

    // Bu açılışta gösterilmesi gereken sürüm notları (yoksa boş liste) ve bu sürümün "görüldü" olarak işaretlenmesi.
    // - Yeni kurulum (daha önce hiç veri yok): pencere gösterilmez.
    // - Güncelleme: son görülen sürümden yeni olan tüm notlar gösterilir.
    // - Not sistemi gelmeden önceki sürümden (1.0.2 ve öncesi) gelen kullanıcı: 1.0.1'den sonraki notlar.
    public static List<Release> TakeUnseen(Version current, bool hasExistingData)
    {
        Version? lastSeen = ReadLastSeen();
        WriteLastSeen(current);

        if (lastSeen is null && !hasExistingData)
            return new List<Release>();

        Version since = lastSeen ?? new Version(1, 0, 1);
        return All.Where(r => r.Version > since && r.Version <= current).ToList();
    }

    // Kurulu sürümün kendi notları (sürüm yazısına tıklayınca, güncelse gösterilir).
    public static List<Release> ForVersion(Version current)
        => All.Where(r => r.Version <= current).Take(1).ToList();

    public static Version ParseVersion(string text)
        => Version.TryParse(text.Split('-', '+')[0], out Version? v) ? Normalize(v) : new Version(0, 0, 0);

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    private static Version? ReadLastSeen()
    {
        try
        {
            return File.Exists(StatePath) && Version.TryParse(File.ReadAllText(StatePath).Trim(), out Version? v)
                ? Normalize(v)
                : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void WriteLastSeen(Version version)
    {
        try
        {
            File.WriteAllText(StatePath, version.ToString(3));
        }
        catch (IOException)
        {
            // Yazılamazsa en kötü ihtimalle pencere bir sonraki açılışta tekrar görünür.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

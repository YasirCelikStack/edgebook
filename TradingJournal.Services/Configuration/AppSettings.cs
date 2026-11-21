using System.Text.Json;

namespace TradingJournal.Services.Configuration;

// Uygulamanın geliştirici ayarları: exe'nin yanındaki appsettings.Local.json dosyasından okunur.
// Bu dosya .gitignore'da; repoda sadece örneği (appsettings.Local.example.json) bulunur.
// Kullanıcı bu değerleri bilmek zorunda değildir; uygulamayı dağıtan kişi bir kez doldurur.
public class AppSettings
{
    public const string FileName = "appsettings.Local.json";

    public ChartVaultSection ChartVault { get; set; } = new();
    public AiSection Ai { get; set; } = new();

    public class AiSection
    {
        // İsteğe bağlı yedek anahtar: kullanıcı kendi Gemini anahtarını girmediyse bu kullanılır.
        public string? GeminiApiKey { get; set; }

        // Boşsa GeminiProvider.DefaultModel; bulunamazsa en güncel Flash modeli otomatik seçilir.
        public string? GeminiModel { get; set; }
    }

    public class ChartVaultSection
    {
        // Chart Vault'un Supabase proje adresi (https://xxxx.supabase.co)
        public string SupabaseUrl { get; set; } = string.Empty;

        // Chart Vault'un Supabase "publishable" anahtarı (sb_publishable_...).
        public string PublishableKey { get; set; } = string.Empty;

        // Chart Vault sitesinin adresi: görsel uç noktası (/api/journal/shots) burada.
        // Gizli bir bilgi olmadığı için varsayılan olarak kodda; appsettings.Local.json'da yazılırsa o kullanılır.
        public const string DefaultSiteUrl = "https://chart-vault-mu.vercel.app";
        public string SiteUrl { get; set; } = DefaultSiteUrl;

        public bool IsSiteConfigured =>
            SiteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !SiteUrl.Contains("BURAYA");

        public bool IsConfigured =>
            SupabaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(PublishableKey)
            && !PublishableKey.Contains("BURAYA");
    }

    private static AppSettings? _current;

    // Uygulama boyunca tek bir kez okunur.
    public static AppSettings Current => _current ??= Load();

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    private static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), options) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }
}

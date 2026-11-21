using System.Text.Json;
using TradingJournal.Services.Configuration;

namespace TradingJournal.Services.Import;

// Chart Vault bağlantısı için gereken bilgiler.
// Supabase adresi ve publishable key uygulamanın ayarıdır (appsettings.Local.json), herkes için aynıdır.
// Kullanıcıya ait olan tek şey e-posta adresidir; kolaylık olsun diye hatırlanır
// (%LocalAppData%\EdgeBook\chartvault.json). Şifre asla kaydedilmez.
public class ChartVaultSettings
{
    public string SupabaseUrl => AppSettings.Current.ChartVault.SupabaseUrl;
    public string PublishableKey => AppSettings.Current.ChartVault.PublishableKey;
    public bool IsConfigured => AppSettings.Current.ChartVault.IsConfigured;
    public string SiteUrl => AppSettings.Current.ChartVault.SiteUrl;
    public bool IsSiteConfigured => AppSettings.Current.ChartVault.IsSiteConfigured;

    public string? Email { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdgeBook", "chartvault.json");

    private sealed record Stored(string? Email);

    public static ChartVaultSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new ChartVaultSettings();
            Stored? stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(FilePath));
            return new ChartVaultSettings { Email = stored?.Email };
        }
        catch (JsonException)
        {
            return new ChartVaultSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new Stored(Email)));
    }
}

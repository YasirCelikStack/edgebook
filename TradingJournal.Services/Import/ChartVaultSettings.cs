using System.Text.Json;

namespace TradingJournal.Services.Import;

// Chart Vault'un Supabase bağlantı bilgileri. Repoya girmez:
// %LocalAppData%\EdgeBook\chartvault.json dosyasında saklanır. Şifre asla kaydedilmez.
public class ChartVaultSettings
{
    // Supabase > Project Settings > API: "Project URL" (https://xxxx.supabase.co)
    public string SupabaseUrl { get; set; } = string.Empty;

    // Supabase "Publishable key" (sb_publishable_...). Tarayıcıda zaten açık olan anahtardır;
    // veriyi koruyan şey tablodaki RLS kuralıdır (herkes sadece kendi kayıtlarını görür).
    public string PublishableKey { get; set; } = string.Empty;

    public string? Email { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SupabaseUrl) && !string.IsNullOrWhiteSpace(PublishableKey);

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdgeBook", "chartvault.json");

    public static ChartVaultSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<ChartVaultSettings>(File.ReadAllText(FilePath)) ?? new()
                : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

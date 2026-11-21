using System.Security.Cryptography;
using System.Text;
using TradingJournal.Services.Configuration;

namespace TradingJournal.UI.Helpers;

// Kullanıcının kendi Gemini API anahtarını saklar.
// Anahtar düz metin olarak değil, Windows DPAPI ile şifrelenmiş olarak yazılır:
// dosya başka bir bilgisayara veya başka bir Windows kullanıcısına kopyalansa bile çözülemez.
public static class AiKeyStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("EdgeBook.Gemini");

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdgeBook", "gemini.key");

    public static bool HasUserKey => File.Exists(FilePath);

    public static string? LoadUserKey()
    {
        if (!File.Exists(FilePath))
            return null;
        try
        {
            byte[] decrypted = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (CryptographicException)
        {
            return null; // Başka kullanıcıdan kopyalanmış ya da bozuk dosya.
        }
    }

    public static void SaveUserKey(string key)
    {
        byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), Entropy, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllBytes(FilePath, encrypted);
    }

    public static void DeleteUserKey()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    // Kullanılacak anahtar: önce kullanıcının kendi anahtarı, yoksa uygulamanın yedek anahtarı.
    public static string? ResolveKey()
        => LoadUserKey() ?? NullIfEmpty(AppSettings.Current.Ai.GeminiApiKey);

    public static string Describe()
    {
        if (LoadUserKey() is not null) return "Kendi Gemini anahtarın kullanılıyor";
        if (NullIfEmpty(AppSettings.Current.Ai.GeminiApiKey) is not null) return "Uygulamanın paylaşılan anahtarı kullanılıyor";
        return "Gemini anahtarı girilmemiş";
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

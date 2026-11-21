using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace TradingJournal.UI.Helpers;

// Uygulama güncellemeleri (Velopack + GitHub Releases).
// Yeni sürüm release.ps1 ile GitHub'a yüklenir; kullanıcının uygulaması açılışta bunu fark eder,
// tek tıkla indirip yeniden başlar. Sadece kurulum dosyasıyla kurulmuş sürümde çalışır;
// Visual Studio'dan çalıştırılan geliştirme sürümünde güncelleme denetlenmez.
public static class AppUpdater
{
    public const string RepoUrl = "https://github.com/YasirCelikStack/edgebook";

    // Örn. "1.0.0" (derleme bilgisi "+abc123" kısmı atılır).
    public static string CurrentVersion
    {
        get
        {
            string? v = Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return (v ?? Application.ProductVersion).Split('+')[0];
        }
    }

    private static UpdateManager CreateManager() => new(new GithubSource(RepoUrl, null, false));

    public static bool IsInstalled
    {
        get
        {
            try { return CreateManager().IsInstalled; }
            catch (Exception) { return false; }
        }
    }

    // Yeni sürüm varsa bilgisini döndürür; yoksa, kurulu değilse ya da internet yoksa null.
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            UpdateManager manager = CreateManager();
            if (!manager.IsInstalled)
                return null;
            return await manager.CheckForUpdatesAsync();
        }
        catch (Exception)
        {
            return null; // Güncelleme denetimi uygulamayı asla durdurmamalı.
        }
    }

    // Güncellemeyi indirir (ilerleme 0-100) ve uygulamayı yeni sürümle yeniden başlatır.
    public static async Task DownloadAndRestartAsync(UpdateInfo update, Action<int>? progress = null)
    {
        UpdateManager manager = CreateManager();
        await manager.DownloadUpdatesAsync(update, progress);
        manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }
}

using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradingJournal.Core.Exceptions;
using TradingJournal.Data;
using TradingJournal.Services.Screenshots;

namespace TradingJournal.Services.DataBackup;

// Yedek dosyasının içindeki bilgi kartı (manifest.json).
public record BackupInfo(string App, string AppVersion, DateTime CreatedAt, int Trades, int Screenshots);

// EdgeBook verisini tek bir .zip dosyasına yedekler ve geri yükler.
// Yedek, kullanıcının seçtiği yere (Masaüstü, USB, Drive klasörü...) kaydedilir; internete hiçbir şey gönderilmez.
//
// Zip'in içi:
//   manifest.json          ne zaman, hangi sürümle, kaç işlem
//   edgebook.db            veritabanının tutarlı bir kopyası
//   screenshots/...        işlemlere eklenmiş ekran görüntüleri (veritabanında göreli yolla kayıtlı)
//   coach-reports.json     son yapay zeka koç raporları (varsa)
//
// Yedeğe KONMAYANLAR: Gemini anahtarı (Windows'un bu kullanıcıya özel şifrelemesiyle saklanır,
// başka bilgisayarda zaten çözülemez) ve Chart Vault e-postası gibi küçük ayarlar.
public class BackupService
{
    private const string AppName = "EdgeBook";
    private const string ManifestEntry = "manifest.json";
    private const string DatabaseEntry = "edgebook.db";
    private const string ScreenshotsPrefix = "screenshots/";
    private const string CoachReportsFile = "coach-reports.json";

    private static string DataFolder => Path.GetDirectoryName(DbContextFactory.DefaultDatabasePath)!;
    private static string AutoBackupFolder => Path.Combine(DataFolder, "backups");

    public async Task<BackupInfo> CreateAsync(string zipPath, string appVersion)
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"edgebook-backup-{Guid.NewGuid():N}.db");
        try
        {
            // Dosyayı doğrudan kopyalamak yerine SQLite'ın kendi yedekleme komutu kullanılır:
            // uygulama açıkken bile tutarlı (yarım yazılmamış) bir kopya alınır.
            CopyDatabase(DbContextFactory.DefaultDatabasePath, tempDb);
            (int trades, int screenshots) = CountRows(tempDb);

            var info = new BackupInfo(AppName, appVersion, DateTime.Now, trades, screenshots);
            string tempZip = zipPath + ".tmp";
            if (File.Exists(tempZip)) File.Delete(tempZip);

            await using (FileStream stream = File.Create(tempZip))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                ZipArchiveEntry manifest = zip.CreateEntry(ManifestEntry);
                await using (Stream entry = manifest.Open())
                    await JsonSerializer.SerializeAsync(entry, info, new JsonSerializerOptions { WriteIndented = true });

                zip.CreateEntryFromFile(tempDb, DatabaseEntry, CompressionLevel.Optimal);

                string coachReports = Path.Combine(DataFolder, CoachReportsFile);
                if (File.Exists(coachReports))
                    zip.CreateEntryFromFile(coachReports, CoachReportsFile);

                string root = ScreenshotService.RootFolder;
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                    // Görseller zaten sıkıştırılmış (PNG/JPG); tekrar sıkıştırmak sadece yavaşlatır.
                    zip.CreateEntryFromFile(file, ScreenshotsPrefix + relative, CompressionLevel.NoCompression);
                }
            }

            // Yarım kalan bir yedek, eski sağlam yedeğin üzerine yazılmasın diye önce geçici dosyaya yazılır.
            File.Move(tempZip, zipPath, overwrite: true);
            return info;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            throw new DataAccessException("Yedek oluşturulamadı.", ex);
        }
        finally
        {
            TryDelete(tempDb);
        }
    }

    // Yedeği açmadan önce içinde ne olduğunu gösterir (onay penceresi için).
    public BackupInfo ReadInfo(string zipPath)
    {
        try
        {
            using ZipArchive zip = ZipFile.OpenRead(zipPath);
            ZipArchiveEntry manifest = zip.GetEntry(ManifestEntry)
                                       ?? throw new DomainException("Bu dosya bir EdgeBook yedeği değil (manifest.json yok).");
            if (zip.GetEntry(DatabaseEntry) is null)
                throw new DomainException("Yedek dosyası eksik: içinde veritabanı yok.");

            using Stream stream = manifest.Open();
            BackupInfo info = JsonSerializer.Deserialize<BackupInfo>(stream)
                              ?? throw new DomainException("Yedek bilgisi okunamadı.");
            if (info.App != AppName)
                throw new DomainException("Bu dosya bir EdgeBook yedeği değil.");
            return info;
        }
        catch (InvalidDataException)
        {
            throw new DomainException("Dosya açılamadı: geçerli bir .zip yedeği değil ya da bozuk.");
        }
        catch (JsonException)
        {
            throw new DomainException("Yedek bilgisi (manifest.json) bozuk.");
        }
    }

    // Mevcut verinin yerine yedeği koyar. Önce mevcut veri otomatik olarak yedeklenir
    // (backups klasörüne), yani geri yükleme de geri alınabilir. Dönen değer: otomatik yedeğin yolu.
    // Bu işlemden sonra uygulama yeniden başlatılmalıdır.
    public async Task<string> RestoreAsync(string zipPath, string appVersion)
    {
        ReadInfo(zipPath); // Geçersiz dosyada hiçbir şeye dokunmadan durur.

        Directory.CreateDirectory(AutoBackupFolder);
        string safetyCopy = Path.Combine(AutoBackupFolder, $"geri-yukleme-oncesi-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        await CreateAsync(safetyCopy, appVersion);

        string tempDir = Path.Combine(Path.GetTempPath(), $"edgebook-restore-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            ExtractSafely(zipPath, tempDir);

            string newDb = Path.Combine(tempDir, DatabaseEntry);
            CountRows(newDb); // Veritabanı gerçekten açılabiliyor mu? Bozuksa burada durur.

            // Açık bağlantı havuzu dosyayı kilitli tutmasın.
            SqliteConnection.ClearAllPools();

            string dbPath = DbContextFactory.DefaultDatabasePath;
            File.Copy(newDb, dbPath, overwrite: true);
            TryDelete(dbPath + "-wal");
            TryDelete(dbPath + "-shm");

            string root = ScreenshotService.RootFolder;
            Directory.Delete(root, recursive: true);
            string newScreenshots = Path.Combine(tempDir, "screenshots");
            Directory.CreateDirectory(root);
            if (Directory.Exists(newScreenshots))
                CopyDirectory(newScreenshots, root); // Taşıma değil kopyalama: geçici klasör başka diskte olabilir.

            string coachReports = Path.Combine(tempDir, CoachReportsFile);
            string coachTarget = Path.Combine(DataFolder, CoachReportsFile);
            if (File.Exists(coachReports))
                File.Copy(coachReports, coachTarget, overwrite: true);
            else
                TryDelete(coachTarget);

            return safetyCopy;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            throw new DataAccessException(
                $"Yedek geri yüklenemedi. Önceki verin şurada yedekli: {safetyCopy}", ex);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); }
            catch (IOException) { }
        }
    }

    // ---------------- Yardımcılar ----------------

    private static void CopyDatabase(string sourcePath, string targetPath)
    {
        // Pooling=False: iş bitince dosyalar hemen serbest kalır (geçici dosya silinebilsin).
        using var source = new SqliteConnection($"Data Source={sourcePath};Mode=ReadOnly;Pooling=False");
        using var target = new SqliteConnection($"Data Source={targetPath};Pooling=False");
        source.Open();
        target.Open();
        source.BackupDatabase(target);
    }

    private static (int Trades, int Screenshots) CountRows(string dbPath)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        int Count(string table)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
            return Convert.ToInt32(command.ExecuteScalar());
        }
        try
        {
            return (Count("Trades"), Count("TradeScreenshots"));
        }
        catch (SqliteException ex)
        {
            throw new DomainException($"Yedekteki veritabanı okunamadı: {ex.Message}");
        }
    }

    // Zip içindeki "../" gibi yollarla hedef klasörün dışına dosya yazılmasını engeller.
    private static void ExtractSafely(string zipPath, string targetDir)
    {
        string root = Path.GetFullPath(targetDir) + Path.DirectorySeparatorChar;
        using ZipArchive zip = ZipFile.OpenRead(zipPath);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // klasör girdisi
            string destination = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new DomainException("Yedek dosyası geçersiz bir yol içeriyor.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

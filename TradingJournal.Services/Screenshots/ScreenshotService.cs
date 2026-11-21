using TradingJournal.Core.Entities;
using TradingJournal.Core.Exceptions;
using TradingJournal.Data;
using TradingJournal.Data.Repositories;

namespace TradingJournal.Services.Screenshots;

// İşlemlere chart ekran görüntüsü ekler/siler.
// Resim dosyası diskte %LocalAppData%\EdgeBook\screenshots\<işlemId>\ klasörüne kopyalanır,
// veritabanına sadece bu klasöre göre göreli yolu yazılır (örn. "12\20261121_153000_a1b2c3.png").
// Böylece veritabanı şişmez ve kullanıcı orijinal dosyayı silse bile görüntü kaybolmaz.
public class ScreenshotService
{
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif"];

    public static string RootFolder
    {
        get
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EdgeBook", "screenshots");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static bool IsSupportedImage(string path)
        => SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    // Veritabanındaki göreli yolu tam dosya yoluna çevirir.
    public static string GetFullPath(TradeScreenshot screenshot)
        => Path.IsPathRooted(screenshot.FilePath)
            ? screenshot.FilePath
            : Path.Combine(RootFolder, screenshot.FilePath);

    public async Task<List<TradeScreenshot>> GetForTradeAsync(int tradeId)
    {
        using var db = DbContextFactory.Create();
        List<TradeScreenshot> list = await new Repository<TradeScreenshot>(db).FindAsync(s => s.TradeId == tradeId);
        return list.OrderBy(s => s.CreatedAt).ToList();
    }

    public async Task<TradeScreenshot> AddFromFileAsync(int tradeId, string sourcePath, string? caption = null)
    {
        if (!File.Exists(sourcePath))
            throw new DomainException("Dosya bulunamadı.");
        if (!IsSupportedImage(sourcePath))
            throw new DomainException("Sadece PNG, JPG, BMP veya GIF resimleri eklenebilir.");

        await using FileStream source = File.OpenRead(sourcePath);
        return await AddFromStreamAsync(tradeId, source, Path.GetExtension(sourcePath), caption);
    }

    // Panodan yapıştırılan resim gibi dosyası olmayan içerikler için.
    public async Task<TradeScreenshot> AddFromStreamAsync(int tradeId, Stream content, string extension, string? caption = null)
    {
        extension = extension.ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
            throw new DomainException("Desteklenmeyen resim türü.");

        string tradeFolder = Path.Combine(RootFolder, tradeId.ToString());
        Directory.CreateDirectory(tradeFolder);

        string fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}{extension}";
        string fullPath = Path.Combine(tradeFolder, fileName);

        await using (FileStream target = File.Create(fullPath))
            await content.CopyToAsync(target);

        var screenshot = new TradeScreenshot
        {
            TradeId = tradeId,
            FilePath = Path.Combine(tradeId.ToString(), fileName),
            Caption = caption
        };

        try
        {
            using var db = DbContextFactory.Create();
            await new Repository<TradeScreenshot>(db).AddAsync(screenshot);
        }
        catch
        {
            // Kayıt başarısızsa diskte sahipsiz dosya bırakmayalım.
            File.Delete(fullPath);
            throw;
        }

        return screenshot;
    }

    public async Task DeleteAsync(TradeScreenshot screenshot)
    {
        using (var db = DbContextFactory.Create())
            await new Repository<TradeScreenshot>(db).DeleteAsync(screenshot.Id);

        string path = GetFullPath(screenshot);
        if (File.Exists(path))
            File.Delete(path);
    }

    // İşlem silinince veritabanı kayıtları cascade ile gider; klasörü de biz sileriz.
    public static void DeleteTradeFolder(int tradeId)
    {
        string folder = Path.Combine(RootFolder, tradeId.ToString());
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }
}

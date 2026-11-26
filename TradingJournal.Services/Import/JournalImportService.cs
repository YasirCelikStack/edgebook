using Microsoft.EntityFrameworkCore;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.Core.Exceptions;
using TradingJournal.Data;
using TradingJournal.Services.Screenshots;

namespace TradingJournal.Services.Import;

// Önizleme satırı: içe aktarılacak işlem + EdgeBook'ta zaten var mı.
public record ImportPreviewItem(ImportedTrade Trade, bool AlreadyImported);

public record ImportResult(int Added, int Skipped, int Failed, List<string> Errors);

public record ShotDownloadResult(int Downloaded, int AlreadyHad, int Failed, List<string> Errors);

// Dış kaynaktan gelen işlemleri EdgeBook işlemlerine çevirip kaydeder.
//
// Lot büyüklüğü: Chart Vault işlemleri sadece R cinsinden tutar (para / lot yok).
// Bu yüzden kullanıcı "işlem başı risk %" verir; lot, stop mesafesinden hesaplanır:
//   risk tutarı = başlangıç bakiyesi × risk %
//   Forex:  lot    = risk tutarı / (stop pip × 1 lotta pip değeri)
//   Kripto: miktar = risk tutarı / |giriş − stop|
// Böylece -1R'lik işlem yaklaşık risk tutarı kadar zarar olarak görünür.
public class JournalImportService
{
    public async Task<List<ImportPreviewItem>> PreviewAsync(IEnumerable<ImportedTrade> trades)
    {
        using var db = DbContextFactory.Create();
        HashSet<string> existing = await LoadExistingIdsAsync(db);
        return trades.Select(t => new ImportPreviewItem(t, existing.Contains(t.ExternalId))).ToList();
    }

    public async Task<ImportResult> ImportAsync(IJournalImporter source, IEnumerable<ImportedTrade> trades,
        int accountId, decimal riskPercent)
    {
        if (riskPercent <= 0 || riskPercent > 100)
            throw new DomainException("İşlem başı risk 0 ile 100 arasında olmalıdır.");

        using var db = DbContextFactory.Create();
        Account account = await db.Accounts.FindAsync(accountId)
                          ?? throw new DomainException("Hesap bulunamadı.");
        decimal riskAmount = account.InitialBalance * riskPercent / 100m;

        HashSet<string> existing = await LoadExistingIdsAsync(db);
        Dictionary<string, Instrument> instruments = await db.Instruments.ToDictionaryAsync(i => i.Symbol);
        Dictionary<string, Strategy> strategies = await db.Strategies.ToDictionaryAsync(s => s.Name, StringComparer.OrdinalIgnoreCase);

        int added = 0, skipped = 0, failed = 0;
        var errors = new List<string>();

        foreach (ImportedTrade item in trades)
        {
            if (existing.Contains(item.ExternalId))
            {
                skipped++;
                continue;
            }

            try
            {
                Instrument instrument = GetOrCreateInstrument(db, instruments, item.Symbol);
                Strategy? strategy = item.Setup is null ? null : GetOrCreateStrategy(db, strategies, item.Setup);

                Trade trade = CreateTrade(item, instrument, riskAmount);
                trade.Account = account;
                trade.Instrument = instrument;
                trade.Strategy = strategy;

                db.Trades.Add(trade);
                existing.Add(item.ExternalId);
                added++;
            }
            catch (DomainException ex)
            {
                failed++;
                errors.Add($"{item.Symbol} {item.OpenTime:dd.MM.yyyy HH:mm}: {ex.Message}");
            }
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new DataAccessException("İçe aktarılan işlemler kaydedilemedi.", ex);
        }

        return new ImportResult(added, skipped, failed, errors);
    }

    // Chart Vault'tan aktarılmış işlemlerin grafik görsellerini indirir ve işlemlere ekran görüntüsü olarak ekler.
    // Daha önce indirilen görseller (açıklamasında "chartvault:<chart_id>" olanlar) tekrar indirilmez.
    // Hem yeni aktarımdan sonra hem de eski işlemler için ayrıca çağrılabilir.
    // onlyExternalIds verilirse sadece o işlemlerin görselleri indirilir (ör. az önce seçilip aktarılanlar).
    public async Task<ShotDownloadResult> DownloadChartVaultShotsAsync(ChartVaultApiImporter importer,
        IProgress<string>? progress = null, IReadOnlyCollection<string>? onlyExternalIds = null, CancellationToken ct = default)
    {
        const string prefix = "chartvault:";
        Dictionary<string, int> tradeIdByJournalId;
        using (var db = DbContextFactory.Create())
        {
            tradeIdByJournalId = (await db.Trades
                    .Where(t => t.ExternalId != null && t.ExternalId.StartsWith(prefix))
                    .Select(t => new { t.Id, t.ExternalId })
                    .ToListAsync(ct))
                .Where(t => onlyExternalIds is null || onlyExternalIds.Contains(t.ExternalId!))
                .ToDictionary(t => t.ExternalId![prefix.Length..], t => t.Id);
        }

        if (tradeIdByJournalId.Count == 0)
            return new ShotDownloadResult(0, 0, 0, new List<string>());

        var screenshots = new ScreenshotService();
        HashSet<string> alreadyDownloaded = await screenshots.GetCaptionsStartingWithAsync(prefix);

        progress?.Report("Görsel listesi alınıyor...");
        List<ChartVaultShot> shots = await importer.FetchShotsAsync(tradeIdByJournalId.Keys.ToList(), ct);

        int downloaded = 0, alreadyHad = 0, failed = 0;
        var errors = new List<string>();
        List<ChartVaultShot> toDownload = shots.Where(s => !alreadyDownloaded.Contains(s.Marker)).ToList();
        alreadyHad = shots.Count - toDownload.Count;

        foreach (ChartVaultShot shot in toDownload)
        {
            if (!tradeIdByJournalId.TryGetValue(shot.JournalId, out int tradeId))
                continue;

            progress?.Report($"Görseller indiriliyor... {downloaded + failed + 1}/{toDownload.Count}");
            try
            {
                (MemoryStream content, string extension) = await importer.DownloadShotAsync(shot, ct);
                using (content)
                    await screenshots.AddFromStreamAsync(tradeId, content, extension, shot.Marker);
                downloaded++;
            }
            catch (Exception ex) when (ex is ImportException or DomainException or DataAccessException or HttpRequestException or IOException)
            {
                failed++;
                errors.Add(ex.Message);
            }
        }

        return new ShotDownloadResult(downloaded, alreadyHad, failed, errors);
    }

    // Önizleme için: hangi Chart Vault kayıtlarının bağlı bir grafik görseli var?
    // Dönen küme ExternalId'lerden ("chartvault:<id>") oluşur. Görsel listesi alınamazsa null döner;
    // bu durumda önizleme yine çalışır, sadece "Görsel" sütunu boş kalır.
    public async Task<HashSet<string>?> GetTradesWithShotsAsync(ChartVaultApiImporter importer,
        IEnumerable<ImportedTrade> trades, CancellationToken ct = default)
    {
        const string prefix = "chartvault:";
        List<string> journalIds = trades
            .Where(t => t.ExternalId.StartsWith(prefix))
            .Select(t => t.ExternalId[prefix.Length..])
            .ToList();
        if (journalIds.Count == 0)
            return new HashSet<string>();

        try
        {
            List<ChartVaultShot> shots = await importer.FetchShotsAsync(journalIds, ct);
            return shots.Select(s => prefix + s.JournalId).ToHashSet();
        }
        catch (Exception ex) when (ex is ImportException or HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    private static async Task<HashSet<string>> LoadExistingIdsAsync(TradingJournalDbContext db)
        => (await db.Trades.Where(t => t.ExternalId != null).Select(t => t.ExternalId!).ToListAsync()).ToHashSet();

    private static Trade CreateTrade(ImportedTrade item, Instrument instrument, decimal riskAmount)
    {
        decimal stopDistance = Math.Abs(item.EntryPrice - item.StopLoss);
        if (stopDistance == 0)
            throw new DomainException("Stop mesafesi sıfır.");

        Trade trade;
        if (instrument.Type == InstrumentType.Crypto)
        {
            trade = new CryptoTrade
            {
                Quantity = Math.Max(0.000001m, Math.Round(riskAmount / stopDistance, 6)),
                Leverage = 1
            };
        }
        else
        {
            decimal stopPips = stopDistance / instrument.PipSize;
            decimal lot = riskAmount / (stopPips * instrument.PipValuePerLot);
            // Lot 0,01'e yuvarlanmaz: Chart Vault R ile çalışır, net sonuç R × risk tutarına eşit çıkmalı.
            // (Eskiden en az 0,01 lot yazılıyordu; küçük lot gereken işlemlerde risk ve kâr onlarca kat büyüyordu.)
            trade = new ForexTrade
            {
                PipSize = instrument.PipSize,
                PipValuePerLot = instrument.PipValuePerLot,
                LotSize = Math.Max(0.000001m, Math.Round(lot, 6))
            };
        }

        trade.ExternalId = item.ExternalId;
        trade.Direction = item.Direction;
        trade.OpenTime = item.OpenTime;
        trade.EntryPrice = item.EntryPrice;
        trade.StopLoss = item.StopLoss;
        trade.TakeProfit = item.TakeProfit;
        trade.Notes = BuildNote(item);

        if (item.IsClosed)
        {
            decimal exit = item.ExitPrice ?? ExitFromR(item, stopDistance);
            DateTime closeTime = item.CloseTime is DateTime c && c >= item.OpenTime ? c : item.OpenTime;
            trade.Close(exit, closeTime);
        }

        return trade;
    }

    // Çıkış fiyatı yoksa (elle kapatılmış) R sonucundan geri hesapla: giriş ± R × stop mesafesi.
    private static decimal ExitFromR(ImportedTrade item, decimal stopDistance)
    {
        if (item.ResultR is not decimal r)
            throw new DomainException("Kapanmış işlemde çıkış fiyatı ve R sonucu yok.");
        int sign = item.Direction == TradeDirection.Long ? 1 : -1;
        return item.EntryPrice + sign * r * stopDistance;
    }

    private static string BuildNote(ImportedTrade item)
    {
        string header = $"[Chart Vault{(item.Timeframe is null ? "" : " · " + item.Timeframe)} · {item.SourceStatus}]";
        string note = string.IsNullOrWhiteSpace(item.Note) ? header : $"{header} {item.Note.Trim()}";
        return note.Length > 4000 ? note[..4000] : note;
    }

    private static Instrument GetOrCreateInstrument(TradingJournalDbContext db, Dictionary<string, Instrument> cache, string symbol)
    {
        if (cache.TryGetValue(symbol, out Instrument? found))
            return found;

        var instrument = new Instrument { Symbol = symbol };
        if (IsCrypto(symbol))
        {
            instrument.Type = InstrumentType.Crypto;
            instrument.PipSize = 0.01m;
            instrument.PipValuePerLot = 1m;
        }
        else
        {
            // Yaygın broker değerleri; farklıysa Tanımlar > Enstrümanlar'dan düzeltilebilir.
            instrument.Type = InstrumentType.Forex;
            (instrument.PipSize, instrument.PipValuePerLot) = ForexSpecFor(symbol);
        }

        db.Instruments.Add(instrument);
        cache[symbol] = instrument;
        return instrument;
    }

    // (pip büyüklüğü, 1 lotta 1 pip'in dolar değeri)
    private static (decimal PipSize, decimal PipValuePerLot) ForexSpecFor(string symbol)
    {
        string s = symbol.ToUpperInvariant();

        // Petrol: 1 lot = 1000 varil, 1 pip = 0,01 $ → lot başına 10 $
        if (s.Contains("OIL") || s.Contains("WTI") || s.Contains("BRENT")
            || s.StartsWith("XTI") || s.StartsWith("XBR") || s.StartsWith("USO") || s.StartsWith("UKO"))
            return (0.01m, 10m);

        // Altın: 1 lot = 100 ons, 1 pip = 0,1 $ → 10 $. Gümüş: 1 lot = 5000 ons, 1 pip = 0,01 $ → 50 $
        if (s.StartsWith("XAU")) return (0.1m, 10m);
        if (s.StartsWith("XAG")) return (0.01m, 50m);

        // Endeksler: 1 lot = 1 kontrat, 1 puan = 1 $ (brokere göre değişir)
        string[] indices = ["US30", "DJ30", "NAS100", "US100", "USTEC", "SPX500", "US500", "GER40", "DE40", "UK100", "JP225"];
        if (indices.Any(s.StartsWith))
            return (1m, 1m);

        return (s.Contains("JPY") ? 0.01m : 0.0001m, 10m);
    }

    private static bool IsCrypto(string symbol)
        => symbol.EndsWith("USDT") || symbol.EndsWith("USDC") || symbol.EndsWith("BUSD")
           || symbol.StartsWith("BTC") || symbol.StartsWith("ETH");

    private static Strategy GetOrCreateStrategy(TradingJournalDbContext db, Dictionary<string, Strategy> cache, string name)
    {
        if (cache.TryGetValue(name, out Strategy? found))
            return found;

        var strategy = new Strategy { Name = name.Length > 100 ? name[..100] : name };
        db.Strategies.Add(strategy);
        cache[name] = strategy;
        return strategy;
    }
}

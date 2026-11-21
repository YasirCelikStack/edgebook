using Microsoft.EntityFrameworkCore;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.Core.Exceptions;
using TradingJournal.Data;
using TradingJournal.Services.Statistics;

namespace TradingJournal.Services.Demo;

// Deneme ve sunum için gerçekçi örnek işlemler üretir.
// Veriler tamamen rastgele değildir; içine bilinçli örüntüler gizlenir ki analiz ekranları
// ve yapay zeka koçu bulacak bir şey bulsun:
//   • Londra seansında (özellikle EURUSD/GBPUSD Long) daha başarılı
//   • NY öğleden sonra killzone'unda Short işlemler ve Cuma günleri zayıf
//   • FOMO / Revenge etiketli işlemler çoğunlukla zararlı, kayıptan sonra risk büyütülüyor
//   • A+ Setup etiketli işlemler daha başarılı
//   • Kazananlar erken kapatılıyor, kaybedenler daha uzun tutuluyor
// Tüm örnek işlemler ayrı bir "örnek veri" hesabına yazılır; silince kullanıcının kendi verisi etkilenmez.
public class DemoDataGenerator
{
    public const string AccountName = "Demo Prop Hesap (örnek veri)";
    private const string DemoMarker = "(örnek veri)";

    private record SymbolInfo(string Symbol, decimal Price, decimal MinStop, decimal MaxStop, int Digits);

    private static readonly SymbolInfo[] Symbols =
    [
        new("EURUSD", 1.0850m, 0.0008m, 0.0025m, 5),
        new("GBPUSD", 1.2700m, 0.0010m, 0.0030m, 5),
        new("USDJPY", 151.20m, 0.10m, 0.30m, 3),
        new("XAUUSD", 2380m, 3m, 9m, 2),
        new("BTCUSDT", 64000m, 350m, 1100m, 2),
        new("ETHUSDT", 3150m, 20m, 60m, 2),
    ];

    private static readonly string[] StrategyNames =
        ["London Sweep + FVG", "NY Breaker Block", "Asya Range Kırılımı", "Trend Devamı"];

    private static readonly string[] WinNotes =
    [
        "Plana uydum, sweep sonrası FVG'den giriş. Sabırla bekledim.",
        "HTF bias ile aynı yönde, temiz giriş.",
        "Likidite alındıktan sonra displacement geldi, girişim net.",
        "Hedefe kadar tuttum, müdahale etmedim."
    ];

    private static readonly string[] EarlyExitNotes =
    [
        "Kâra geçince korktum, hedef gelmeden kapattım.",
        "Fiyat biraz geri çekilince erken çıktım, sonra hedefe gitti."
    ];

    private static readonly string[] LossNotes =
    [
        "Bias yanlıştı, stop oldu. Kabul.",
        "Giriş biraz erkendi, onay beklemedim.",
        "Stopu çok dar koydum.",
        "Setup geçerliydi ama piyasa ters gitti."
    ];

    private static readonly string[] FomoNotes =
    [
        "Hareketi kaçırdım diye geç girdim. FOMO.",
        "Setup yoktu, sıkıldığım için girdim."
    ];

    private static readonly string[] RevengeNotes =
    [
        "Önceki kaybı çıkarmak için girdim, lot büyüttüm. Hata.",
        "Sinirliydim, plansız işlem."
    ];

    public async Task<bool> ExistsAsync()
    {
        using var db = DbContextFactory.Create();
        return await db.Accounts.AnyAsync(a => a.Name == AccountName);
    }

    public async Task<int> GenerateAsync(int tradeCount = 180, int seed = 42)
    {
        using var db = DbContextFactory.Create();
        if (await db.Accounts.AnyAsync(a => a.Name == AccountName))
            throw new DomainException("Örnek veri zaten var. Yeniden oluşturmak için önce \"Örnek verileri sil\"e bas.");

        var random = new Random(seed);

        var account = new Account
        {
            Name = AccountName,
            Broker = "Örnek Prop Firm",
            Type = AccountType.PropFirm,
            Currency = "USD",
            InitialBalance = 100_000m
        };
        account.Rules.Add(new AccountRule { RuleType = RiskRuleType.DailyLoss, LimitPercent = 5 });
        account.Rules.Add(new AccountRule { RuleType = RiskRuleType.MaxDrawdown, LimitPercent = 10 });
        db.Accounts.Add(account);

        Dictionary<string, Instrument> instruments = await GetInstrumentsAsync(db);
        Dictionary<string, Strategy> strategies = await GetStrategiesAsync(db);
        Dictionary<string, Tag> tags = await GetTagsAsync(db);

        DateTime start = DateTime.Today.AddDays(-120);
        bool previousLoss = false;

        // İşlem zamanlarını önce üret, sırala: "kayıptan sonra revenge" örüntüsü sıralı olmalı.
        var times = Enumerable.Range(0, tradeCount)
            .Select(_ => PickOpenTime(random, start))
            .OrderBy(x => x.OpenTime)
            .ToList();

        for (int i = 0; i < times.Count; i++)
        {
            (DateTime openTime, TradingSession session) = times[i];
            SymbolInfo info = PickSymbol(random, session);
            Instrument instrument = instruments[info.Symbol];
            TradeDirection direction = random.NextDouble() < 0.55 ? TradeDirection.Long : TradeDirection.Short;
            Killzone killzone = SessionClassifier.GetKillzone(openTime);

            // --- Etiketler ve davranış ---
            bool isRevenge = previousLoss && random.NextDouble() < 0.3;
            bool isFomo = !isRevenge && random.NextDouble() < 0.12;
            bool isAPlus = !isRevenge && !isFomo && random.NextDouble() < 0.2;
            bool isNews = random.NextDouble() < 0.08;

            // --- Kazanma olasılığı: gizli örüntüler burada ---
            double winChance = 0.44;
            if (session == TradingSession.London) winChance += 0.14;
            if (session == TradingSession.London && direction == TradeDirection.Long && info.Symbol is "EURUSD" or "GBPUSD") winChance += 0.1;
            if (killzone == Killzone.NewYorkPm && direction == TradeDirection.Short) winChance -= 0.22;
            if (openTime.DayOfWeek == DayOfWeek.Friday) winChance -= 0.12;
            if (session == TradingSession.Asia && info.Symbol == "XAUUSD") winChance -= 0.1;
            if (isFomo || isRevenge) winChance -= 0.25;
            if (isAPlus) winChance += 0.2;
            if (isNews) winChance -= 0.1;
            winChance = Math.Clamp(winChance, 0.08, 0.85);

            decimal plannedRR = (decimal)(1.5 + random.NextDouble() * 1.5);
            double roll = random.NextDouble();
            bool earlyExit = false;
            decimal resultR;
            if (roll < 0.05)
                resultR = 0; // başa baş
            else if (random.NextDouble() < winChance)
            {
                earlyExit = random.NextDouble() < 0.3;
                resultR = earlyExit ? (decimal)(0.4 + random.NextDouble() * 0.6) : plannedRR;
            }
            else
                resultR = random.NextDouble() < 0.9 ? -1m : -(decimal)(0.3 + random.NextDouble() * 0.5);

            // Kazananlar erken, kaybedenler geç kapatılıyor.
            double holdMinutes = resultR > 0
                ? (earlyExit ? 10 + random.NextDouble() * 30 : 25 + random.NextDouble() * 100)
                : 45 + random.NextDouble() * 260;

            decimal riskPercent = isRevenge ? 2m : session == TradingSession.Asia ? 0.5m : 1m;
            decimal riskAmount = account.InitialBalance * riskPercent / 100m;

            // --- Fiyatlar ---
            decimal entry = Round(info.Price * (decimal)(0.97 + random.NextDouble() * 0.06), info.Digits);
            decimal stopDistance = Round(info.MinStop + (info.MaxStop - info.MinStop) * (decimal)random.NextDouble(), info.Digits);
            int sign = direction == TradeDirection.Long ? 1 : -1;

            Trade trade = CreateTrade(instrument, riskAmount, stopDistance);
            trade.Account = account;
            trade.Instrument = instrument;
            trade.Strategy = strategies[PickStrategy(random, session)];
            trade.Direction = direction;
            trade.OpenTime = openTime;
            trade.EntryPrice = entry;
            trade.StopLoss = Round(entry - sign * stopDistance, info.Digits);
            trade.TakeProfit = Round(entry + sign * stopDistance * plannedRR, info.Digits);
            trade.ExternalId = $"demo:{Guid.NewGuid():N}";
            if (trade is ForexTrade forex)
                trade.Commission = Math.Round(forex.LotSize * 7m, 2);

            if (isRevenge) trade.Tags.Add(tags["Revenge"]);
            if (isFomo) trade.Tags.Add(tags["FOMO"]);
            if (isAPlus) trade.Tags.Add(tags["A+ Setup"]);
            if (isNews) trade.Tags.Add(tags["Haber Günü"]);
            if (earlyExit) trade.Tags.Add(tags["Erken Çıkış"]);

            trade.Notes = PickNote(random, resultR, earlyExit, isFomo, isRevenge);

            // Son birkaç işlem açık kalsın.
            bool keepOpen = i >= times.Count - 3;
            if (!keepOpen)
            {
                decimal exit = Round(entry + sign * stopDistance * resultR, info.Digits);
                trade.Close(exit, openTime.AddMinutes(holdMinutes));
                previousLoss = resultR < 0;
            }

            // Uygulama puanı sonuca göre değil davranışa göre verilir (plana uyuldu mu?).
            // Sonuca göre verilseydi "4-5 yıldızlıların hepsi kazanmış" gibi yanıltıcı bir ilişki oluşurdu.
            trade.Rating = isAPlus ? random.Next(4, 6)
                : isFomo || isRevenge ? random.Next(1, 3)
                : random.Next(2, 5);

            db.Trades.Add(trade);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new DataAccessException("Örnek veriler kaydedilemedi.", ex);
        }

        return times.Count;
    }

    // Örnek hesabı (cascade ile tüm işlemleriyle) ve artık kullanılmayan örnek stratejileri siler.
    public async Task<bool> DeleteAsync()
    {
        using var db = DbContextFactory.Create();
        Account? account = await db.Accounts.FirstOrDefaultAsync(a => a.Name == AccountName);
        if (account is null)
            return false;

        db.Accounts.Remove(account);
        await db.SaveChangesAsync();

        var unusedDemoStrategies = await db.Strategies
            .Where(s => s.Description == DemoMarker && !s.Trades.Any())
            .ToListAsync();
        db.Strategies.RemoveRange(unusedDemoStrategies);
        await db.SaveChangesAsync();
        return true;
    }

    // ---------------- Yardımcılar ----------------

    private static (DateTime OpenTime, TradingSession Session) PickOpenTime(Random random, DateTime start)
    {
        // Seans ağırlıkları: Londra ve Londra+NY en çok işlem yapılan dilimler.
        double s = random.NextDouble();
        (int fromMinute, int toMinute) = s switch
        {
            < 0.15 => (20 * 60, 23 * 60 + 59),   // Asya (NY 20:00-24:00)
            < 0.50 => (3 * 60, 8 * 60),          // Londra
            < 0.80 => (8 * 60, 12 * 60),         // Londra + NY
            _ => (12 * 60, 16 * 60 + 30)         // New York (öğleden sonra killzone dahil)
        };

        DateTime day;
        do
        {
            day = start.AddDays(random.Next(0, 120));
        } while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);

        DateTime ny = day.Date.AddMinutes(random.Next(fromMinute, toMinute));
        DateTime local = SessionClassifier.FromNewYorkTime(ny);
        if (local > DateTime.Now.AddHours(-6))
            local = DateTime.Now.AddDays(-1).Date.AddHours(11);
        return (local, SessionClassifier.GetSession(local));
    }

    private static SymbolInfo PickSymbol(Random random, TradingSession session)
    {
        double r = random.NextDouble();
        if (r < 0.12) return Symbols[4];                      // BTCUSDT
        if (r < 0.18) return Symbols[5];                      // ETHUSDT
        if (session == TradingSession.Asia && r < 0.45) return Symbols[2]; // Asya'da USDJPY
        return r switch
        {
            < 0.48 => Symbols[0],   // EURUSD
            < 0.68 => Symbols[1],   // GBPUSD
            < 0.80 => Symbols[3],   // XAUUSD
            _ => Symbols[2]         // USDJPY
        };
    }

    private static string PickStrategy(Random random, TradingSession session) => session switch
    {
        TradingSession.London => random.NextDouble() < 0.75 ? StrategyNames[0] : StrategyNames[3],
        TradingSession.LondonNewYorkOverlap or TradingSession.NewYork => random.NextDouble() < 0.65 ? StrategyNames[1] : StrategyNames[3],
        _ => random.NextDouble() < 0.7 ? StrategyNames[2] : StrategyNames[3]
    };

    private static string? PickNote(Random random, decimal resultR, bool earlyExit, bool fomo, bool revenge)
    {
        if (revenge) return RevengeNotes[random.Next(RevengeNotes.Length)];
        if (fomo) return FomoNotes[random.Next(FomoNotes.Length)];
        if (random.NextDouble() > 0.45) return null;
        if (earlyExit) return EarlyExitNotes[random.Next(EarlyExitNotes.Length)];
        return resultR > 0 ? WinNotes[random.Next(WinNotes.Length)] : LossNotes[random.Next(LossNotes.Length)];
    }

    // Lot / miktar, risk tutarı stop mesafesine eşit olacak şekilde hesaplanır.
    private static Trade CreateTrade(Instrument instrument, decimal riskAmount, decimal stopDistance)
    {
        if (instrument.Type == InstrumentType.Crypto)
            return new CryptoTrade { Quantity = Math.Max(0.0001m, Math.Round(riskAmount / stopDistance, 4)), Leverage = 5 };

        decimal stopPips = stopDistance / instrument.PipSize;
        return new ForexTrade
        {
            PipSize = instrument.PipSize,
            PipValuePerLot = instrument.PipValuePerLot,
            LotSize = Math.Max(0.01m, Math.Round(riskAmount / (stopPips * instrument.PipValuePerLot), 2))
        };
    }

    private static decimal Round(decimal value, int digits) => Math.Round(value, digits);

    private static async Task<Dictionary<string, Instrument>> GetInstrumentsAsync(TradingJournalDbContext db)
    {
        Dictionary<string, Instrument> existing = await db.Instruments.ToDictionaryAsync(i => i.Symbol);
        foreach (SymbolInfo s in Symbols.Where(s => !existing.ContainsKey(s.Symbol)))
        {
            bool crypto = s.Symbol.EndsWith("USDT");
            var instrument = new Instrument
            {
                Symbol = s.Symbol,
                Type = crypto ? InstrumentType.Crypto : InstrumentType.Forex,
                PipSize = crypto ? 0.01m : s.Symbol.Contains("JPY") ? 0.01m : s.Symbol.StartsWith("XAU") ? 0.1m : 0.0001m,
                PipValuePerLot = crypto ? 1m : 10m
            };
            db.Instruments.Add(instrument);
            existing[s.Symbol] = instrument;
        }
        return existing;
    }

    private static async Task<Dictionary<string, Strategy>> GetStrategiesAsync(TradingJournalDbContext db)
    {
        Dictionary<string, Strategy> existing = await db.Strategies.ToDictionaryAsync(s => s.Name);
        foreach (string name in StrategyNames.Where(n => !existing.ContainsKey(n)))
        {
            var strategy = new Strategy { Name = name, Description = DemoMarker };
            db.Strategies.Add(strategy);
            existing[name] = strategy;
        }
        return existing;
    }

    private static async Task<Dictionary<string, Tag>> GetTagsAsync(TradingJournalDbContext db)
    {
        string[] needed = ["A+ Setup", "FOMO", "Revenge", "Haber Günü", "Erken Çıkış"];
        Dictionary<string, Tag> existing = await db.Tags.ToDictionaryAsync(t => t.Name);
        foreach (string name in needed.Where(n => !existing.ContainsKey(n)))
        {
            var tag = new Tag { Name = name };
            db.Tags.Add(tag);
            existing[name] = tag;
        }
        return existing;
    }
}

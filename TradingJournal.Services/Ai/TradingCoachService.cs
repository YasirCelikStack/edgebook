using System.Text.Json;
using TradingJournal.Core.Entities;

namespace TradingJournal.Services.Ai;

// Kaydedilmiş koç raporu: aynı rapor için tekrar istek atıp kota harcamamak için.
public record CoachReport(string ScopeKey, DateTime CreatedAt, string Provider, string Text);

// Yapay zeka koçu: işlem verisinden rapor üretir ve soruları cevaplar.
// Hangi modelin kullanıldığını bilmez; IAiProvider arayüzü üzerinden konuşur.
public class TradingCoachService
{
    private const string SystemPrompt =
        """
        Sen deneyimli bir prop firm risk yöneticisi ve trading koçusun. Kullanıcı forex/kripto işlemlerini
        EdgeBook adlı bir işlem günlüğüne kaydediyor; sana işlemlerinin hazır hesaplanmış istatistikleri verilecek.

        Kurallar:
        - Sadece verilen verilere dayan. Verilerde olmayan bir şeyi uydurma; bilmiyorsan "veride yok" de.
        - Her tespiti sayıyla destekle (işlem sayısı, win rate, ortalama R, süre).
        - Sayıları verilerden olduğu gibi al; verilerde olmayan bir oranı veya R değerini kendin hesaplamaya çalışma.
        - 5'ten az işlemli gruplar için kesin hüküm verme; "örneklem küçük" diye belirt.
        - Fiyat tahmini, al-sat sinyali veya yatırım tavsiyesi verme. Davranış, risk yönetimi ve süreç önerileri ver.
        - Türkçe, sade ve samimi yaz; kullanıcıya "sen" diye hitap et.
        - Markdown kullanma (yıldız, diyez, tablo yok). Başlıkları BÜYÜK HARFLE yaz, maddeler için "•" kullan.
        """;

    private const string ReportRequest =
        """
        Aşağıdaki verilere göre bana bir koç raporu yaz. Şu başlıklar olsun:
        GÜÇLÜ YANLARIN (en fazla 3 madde)
        ZAYIF NOKTALARIN (en fazla 3 madde; seans, sembol, yön, killzone, gün veya etiket kırılımlarından)
        DAVRANIŞ (işlemde kalma süresi, kazananları erken / kaybedenleri geç kapatma, risk büyüklüğü, seriler)
        ÖNÜMÜZDEKİ HAFTA İÇİN 3 KURAL (somut ve ölçülebilir; örn. "Cuma NY öğleden sonra işlem açma")
        TEK CÜMLELİK ÖZET
        Toplam 350 kelimeyi geçme.

        VERİLER:
        """;

    private readonly IAiProvider _provider;
    private readonly TradingContextBuilder _contextBuilder = new();

    public TradingCoachService(IAiProvider provider)
    {
        _provider = provider;
    }

    public async Task<CoachReport> CreateReportAsync(IReadOnlyCollection<Trade> trades, string scopeKey, string scopeName,
        CancellationToken cancellationToken = default)
    {
        EnsureEnoughData(trades);
        string context = _contextBuilder.Build(trades, scopeName);
        string text = await _provider.GenerateAsync(SystemPrompt, [new ChatMessage(true, ReportRequest + context)], cancellationToken);

        var report = new CoachReport(scopeKey, DateTime.Now, _provider.Name, Clean(text));
        CoachReportStore.Save(report);
        return report;
    }

    // Soru-cevap. Önceki mesajlar da gönderilir; böylece "peki ya Londra?" gibi devam soruları anlaşılır.
    public async Task<string> AskAsync(IReadOnlyCollection<Trade> trades, string scopeName, IReadOnlyList<ChatMessage> history,
        string question, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new AiException("Bir soru yaz.");
        EnsureEnoughData(trades);

        string context = _contextBuilder.Build(trades, scopeName);
        var messages = new List<ChatMessage>
        {
            new(true, "İşlem verilerim aşağıda. Sorularımı bu verilere göre kısa (en fazla 8-10 cümle) cevapla.\n\n" + context),
            new(false, "Tamam, verilerini inceledim. Sorunu sorabilirsin.")
        };
        messages.AddRange(history.TakeLast(6));
        messages.Add(new ChatMessage(true, question.Trim()));

        return Clean(await _provider.GenerateAsync(SystemPrompt, messages, cancellationToken));
    }

    private static void EnsureEnoughData(IReadOnlyCollection<Trade> trades)
    {
        if (trades.Count(t => t.IsClosed) < 3)
            throw new AiException("Yorum yapabilmem için en az 3 kapanmış işlem gerekiyor.");
    }

    // Model kurala rağmen markdown kullanırsa temizle.
    private static string Clean(string text)
        => text.Replace("**", "").Replace("##", "").Replace("# ", "").Replace("* ", "• ").Trim();
}

// Son raporları %LocalAppData%\EdgeBook\coach-reports.json dosyasında saklar (hesap başına bir rapor).
public static class CoachReportStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdgeBook", "coach-reports.json");

    public static CoachReport? Load(string scopeKey)
        => LoadAll().GetValueOrDefault(scopeKey);

    public static void Save(CoachReport report)
    {
        Dictionary<string, CoachReport> all = LoadAll();
        all[report.ScopeKey] = report;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Dictionary<string, CoachReport> LoadAll()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Dictionary<string, CoachReport>>(File.ReadAllText(FilePath)) ?? new()
                : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }
}

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingJournal.Services.Ai;

// Google Gemini REST API istemcisi (generateContent uç noktası).
//
// Model seçimi: Model adları sık değişir ve popüler modeller zaman zaman "yoğun" (503) hatası verir.
// Bu yüzden ayarlanan model bulunamazsa (404), yoğunsa (5xx), o modelin kotası dolmuşsa (429) ya da
// zamanında cevap vermezse; hesabın erişebildiği modeller listelenir ve sıradaki uygun model denenir
// (en fazla 3 model). Cevap veren model hatırlanır, sonraki isteklerde önce o kullanılır.
public class GeminiProvider : IAiProvider
{
    // Varsayılan model: 21 Kasım'da ücretsiz katmanda cevap veren model (3.8 ve 3.7 o gün yoğundu).
    // Yoğunsa ya da bulunamazsa hesabın erişebildiği diğer Flash modelleri sırayla denenir;
    // cevap veren model diske kaydedilir ve sonraki açılışlarda önce o kullanılır.
    public const string DefaultModel = "gemini-3.6-flash";

    // Dakikalık sınıra takılınca Gemini'nin söylediği süre bu kadar ya da daha kısaysa beklenip tekrar denenir.
    private static readonly TimeSpan MaxRetryWait = TimeSpan.FromSeconds(40);
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private const int MaxModelAttempts = 3;
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan TestAttemptTimeout = TimeSpan.FromSeconds(20);

    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(180) };

    // Yeni Flash modelleri cevaptan önce "düşünür"; bu, cevabı çok geciktirebilir.
    // Koç yorumu için derin düşünmeye gerek yok, düşünme seviyesi "low" istenir.
    // Model bu ayarı tanımazsa (400) bir daha gönderilmez.
    private static bool _thinkingConfigUnsupported;

    // Son başarılı model: önce bu denenir. Diske de yazılır ki uygulama yeniden açılınca
    // yoğun modelleri tek tek deneyip dakikalarca beklenmesin.
    private static string? _workingModel = LoadWorkingModel();

    private static string WorkingModelFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdgeBook", "gemini-model.txt");

    private static string? LoadWorkingModel()
    {
        try
        {
            return File.Exists(WorkingModelFile) ? File.ReadAllText(WorkingModelFile).Trim() is { Length: > 0 } m ? m : null : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void RememberWorkingModel(string model)
    {
        if (_workingModel == model) return;
        _workingModel = model;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WorkingModelFile)!);
            File.WriteAllText(WorkingModelFile, model);
        }
        catch (IOException)
        {
            // Kaydedilemezse sadece bu oturumda hatırlanır.
        }
    }

    private readonly string _apiKey;
    private readonly string _model;

    public GeminiProvider(string? apiKey, string? model = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AiAuthenticationException("Gemini API anahtarı girilmemiş. Koç sayfasındaki \"Anahtar ayarla\" düğmesini kullan.");
        _apiKey = apiKey.Trim();
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
    }

    public string Name => $"Google Gemini ({_workingModel ?? _model})";

    public async Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (string text, _, _) = await GenerateWithFallbackAsync(systemPrompt, messages, cancellationToken);
            return text;
        }
        catch (HttpRequestException ex)
        {
            throw new AiException("Gemini'ye bağlanılamadı. İnternet bağlantını kontrol et.", ex);
        }
    }

    // Modelleri sırayla dener. Dönen değer: cevap, cevap veren model ve denenen modeller.
    // Bir model yoğunsa (5xx), zaman aşımına uğrarsa, bulunamazsa veya kotası yoksa beklemeden sıradakine geçilir.
    // Tek istisna dakikalık sınır: Gemini'nin söylediği süre kısaysa aynı model bir kez daha denenir.
    private async Task<(string Text, string Model, List<string> Tried)> GenerateWithFallbackAsync(
        string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken ct, TimeSpan? attemptTimeout = null)
    {
        TimeSpan timeout = attemptTimeout ?? AttemptTimeout;
        var tried = new List<string>();
        var errors = new List<string>();     // Kullanıcıya gösterilecek: her model ne cevap verdi.
        List<string>? candidates = null;
        string? model = _workingModel ?? _model;
        Exception? firstError = null;
        bool allQuota = true;

        while (model is not null && tried.Count < MaxModelAttempts)
        {
            tried.Add(model);
            int retries = 0;

            while (true)
            {
                try
                {
                    using var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    attempt.CancelAfter(timeout);
                    string text = await SendAsync(model, systemPrompt, messages, attempt.Token);
                    RememberWorkingModel(model);
                    return (text, model, tried);
                }
                catch (QuotaExceededForModelException quota) when (!quota.IsDaily && retries == 0 && quota.RetryAfter <= MaxRetryWait)
                {
                    // Dakikalık sınır: söylenen süre kadar bekleyip aynı modeli bir kez daha dene.
                    retries++;
                    await Task.Delay(quota.RetryAfter + TimeSpan.FromSeconds(1), ct);
                }
                catch (Exception ex) when (IsRetryableWithAnotherModel(ex, ct))
                {
                    firstError ??= ex;
                    errors.Add($"{model}: {Describe(ex)}");
                    if (ex is not QuotaExceededForModelException) allQuota = false;
                    break;
                }
            }

            candidates ??= await ListCandidateModelsAsync(ct);
            model = candidates.FirstOrDefault(c => !tried.Contains(c));
        }

        string detail = string.Join(" | ", errors);
        if (allQuota && errors.Count > 0)
            throw new AiQuotaExceededException(daily: true, detail);
        throw firstError switch
        {
            OperationCanceledException => new AiException($"Gemini zamanında yanıt vermedi. Biraz sonra tekrar dene.\n({detail})"),
            ServiceUnavailableException => new AiException($"Gemini şu an yoğun. Birkaç dakika sonra tekrar dene.\n({detail})"),
            _ => new AiException($"Gemini'den cevap alınamadı.\n({detail})")
        };
    }

    private static string Describe(Exception ex) => ex switch
    {
        QuotaExceededForModelException q => (q.IsDaily ? "kota yok / günlük kota doldu" : "dakikalık sınır") + $" [{q.Detail}]",
        ServiceUnavailableException u => $"yoğun / sunucu hatası ({u.StatusCode})",
        ModelNotFoundException => "model bulunamadı (404)",
        OperationCanceledException => "zamanında cevap vermedi",
        _ => ex.Message
    };

    // Başka bir modelle tekrar denemeye değer hatalar. Anahtar hatası gibi diğer durumlar doğrudan kullanıcıya gider.
    private static bool IsRetryableWithAnotherModel(Exception ex, CancellationToken userToken) => ex switch
    {
        ModelNotFoundException or ServiceUnavailableException or QuotaExceededForModelException => true,
        OperationCanceledException => !userToken.IsCancellationRequested,
        _ => false
    };

    private async Task<string> SendAsync(string model, string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken ct)
    {
        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = messages.Select(m => new
            {
                role = m.FromUser ? "user" : "model",
                parts = new[] { new { text = m.Text } }
            }).ToArray(),
            generationConfig = _thinkingConfigUnsupported
                ? (object)new { temperature = 0.4 }
                : new { temperature = 0.4, thinkingConfig = new { thinkingLevel = "low" } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/models/{model}:generateContent")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("x-goog-api-key", _apiKey);

        using HttpResponseMessage response = await SharedHttp.SendAsync(request, ct);
        string json = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode == HttpStatusCode.BadRequest && !_thinkingConfigUnsupported
            && json.Contains("thinking", StringComparison.OrdinalIgnoreCase))
        {
            _thinkingConfigUnsupported = true;
            return await SendAsync(model, systemPrompt, messages, ct);
        }

        if (!response.IsSuccessStatusCode)
            throw ToException(response.StatusCode, json);

        GenerateResponse? result = JsonSerializer.Deserialize<GenerateResponse>(json);
        if (result?.PromptFeedback?.BlockReason is string reason)
            throw new AiException($"Gemini bu isteği yanıtlamadı ({reason}).");

        Candidate? candidate = result?.Candidates?.FirstOrDefault();
        var text = new StringBuilder();
        foreach (Part part in candidate?.Content?.Parts ?? [])
        {
            if (part.Thought == true) continue; // Modelin iç düşünme adımları gösterilmez.
            text.Append(part.Text);
        }

        if (text.Length == 0)
            throw new ServiceUnavailableException(200); // Boş cevap: tekrar dene.
        if (candidate?.FinishReason == "MAX_TOKENS")
            text.Append(" …");

        return text.ToString().Trim();
    }

    private static Exception ToException(HttpStatusCode status, string json)
    {
        string? apiMessage = null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            apiMessage = doc.RootElement.GetProperty("error").GetProperty("message").GetString();
        }
        catch (Exception)
        {
            // Hata gövdesi beklenen biçimde değil; sadece durum kodunu kullan.
        }

        return status switch
        {
            // Ücretsiz kota model başınadır. Günlük sınır dolduysa başka model denenebilir;
            // dakikalık sınırsa kısa bir beklemeden sonra aynı model tekrar denenir.
            HttpStatusCode.TooManyRequests => ParseQuota(json, apiMessage),
            HttpStatusCode.NotFound => new ModelNotFoundException(),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new AiAuthenticationException("Gemini anahtarı yetkisiz. Anahtarı AI Studio'dan kontrol et."),
            HttpStatusCode.BadRequest when apiMessage?.Contains("API key", StringComparison.OrdinalIgnoreCase) == true =>
                new AiAuthenticationException("Gemini anahtarı geçersiz. AI Studio'dan kopyaladığın anahtarı tekrar gir."),
            >= HttpStatusCode.InternalServerError => new ServiceUnavailableException((int)status),
            _ => new AiException($"Gemini hatası ({(int)status}): {apiMessage ?? "bilinmeyen hata"}")
        };
    }

    // Sorunun hangi adımda olduğunu bulmak için adım adım bağlantı testi:
    //   1) Model listesi (anahtar geçerli mi, Google'a ulaşılıyor mu?)  2) Kısa bir deneme cevabı.
    public async Task<string> DiagnoseAsync(CancellationToken cancellationToken = default)
    {
        var report = new StringBuilder();
        var watch = Stopwatch.StartNew();

        // 1. adım: model listesi
        try
        {
            using var step = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            step.CancelAfter(TimeSpan.FromSeconds(20));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/models?pageSize=200");
            request.Headers.Add("x-goog-api-key", _apiKey);
            using HttpResponseMessage response = await SharedHttp.SendAsync(request, step.Token);
            string json = await response.Content.ReadAsStringAsync(step.Token);
            if (!response.IsSuccessStatusCode)
            {
                Exception error = ToException(response.StatusCode, json);
                return $"✗ 1) Google'a ulaşıldı ama istek reddedildi ({(int)response.StatusCode}): {error.Message}";
            }

            int count = (JsonSerializer.Deserialize<ModelList>(json)?.Models ?? [])
                .Count(m => m.Methods?.Contains("generateContent") == true);
            report.AppendLine($"✓ 1) Google'a bağlanıldı, anahtar geçerli ({watch.Elapsed.TotalSeconds:0.0} sn). " +
                              $"Kullanılabilir model sayısı: {count}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return "✗ 1) Google'ın sunucusuna 20 saniyede bağlanılamadı. İnternet, güvenlik duvarı, antivirüs veya VPN " +
                   "bağlantıyı engelliyor olabilir.";
        }
        catch (HttpRequestException ex)
        {
            return $"✗ 1) Google'ın sunucusuna bağlanılamadı: {ex.InnerException?.Message ?? ex.Message}";
        }

        // 2. adım: kısa deneme (gerekirse başka modellere geçerek)
        watch.Restart();
        try
        {
            (string answer, string model, List<string> tried) = await GenerateWithFallbackAsync(
                "Kısa cevap ver.", [new ChatMessage(true, "Sadece 'Bağlantı çalışıyor' yaz.")], cancellationToken, TestAttemptTimeout);
            if (tried.Count > 1)
                report.AppendLine($"  Yoğun/erişilemeyen modeller atlandı: {string.Join(", ", tried.Take(tried.Count - 1))}");
            report.AppendLine($"✓ 2) {model} cevap verdi ({watch.Elapsed.TotalSeconds:0.0} sn): \"{answer}\"");
        }
        catch (AiException ex)
        {
            report.AppendLine($"✗ 2) Deneme cevabı alınamadı: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            report.AppendLine($"✗ 2) Bağlantı koptu: {ex.InnerException?.Message ?? ex.Message}");
        }

        return report.ToString().TrimEnd();
    }

    // Hesabın erişebildiği, metin üretebilen modelleri tercih sırasıyla döndürür:
    // önce kararlı Flash modelleri (en yeni önce), sonra önizleme Flash'lar, en son Flash-Lite.
    private async Task<List<string>> ListCandidateModelsAsync(CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/models?pageSize=200");
            request.Headers.Add("x-goog-api-key", _apiKey);
            using HttpResponseMessage response = await SharedHttp.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return new List<string>();

            ModelList? list = await response.Content.ReadFromJsonAsync<ModelList>(ct);
            // "omni" modellerinin ücretsiz katmanda hakkı yok (21 Kasım denemesi); "latest" takma adları zaten aynı modele gider.
            string[] excluded = ["image", "tts", "audio", "live", "embedding", "vision", "latest", "omni"];

            return (list?.Models ?? [])
                .Where(m => m.Methods?.Contains("generateContent") == true)
                .Select(m => m.Name.Replace("models/", ""))
                .Where(n => n.Contains("flash") && !excluded.Any(word => n.Contains(word)))
                .OrderBy(n => n.Contains("lite"))
                .ThenBy(n => n.Contains("preview") || n.Contains("exp"))
                .ThenByDescending(n => n, StringComparer.Ordinal)
                .ToList();
        }
        catch (HttpRequestException)
        {
            return new List<string>();
        }
    }

    // 429 cevabından hangi kotanın aşıldığını ve ne kadar beklenmesi gerektiğini çıkarır.
    // Gemini hata gövdesindeki details[] dizisinde şunlar bulunur:
    //   QuotaFailure.violations[].quotaId  → örn. "GenerateRequestsPerDayPerProjectPerModel-FreeTier"
    //   RetryInfo.retryDelay               → örn. "17s"
    private static QuotaExceededForModelException ParseQuota(string json, string? apiMessage)
    {
        var quotaIds = new List<string>();
        TimeSpan retryAfter = TimeSpan.FromSeconds(20);
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetProperty("error").TryGetProperty("details", out JsonElement details))
            {
                foreach (JsonElement detail in details.EnumerateArray())
                {
                    if (detail.TryGetProperty("violations", out JsonElement violations))
                        foreach (JsonElement v in violations.EnumerateArray())
                            if (v.TryGetProperty("quotaId", out JsonElement id) && id.GetString() is string quotaId)
                                quotaIds.Add(quotaId);

                    if (detail.TryGetProperty("retryDelay", out JsonElement delay)
                        && delay.GetString() is string text
                        && double.TryParse(text.TrimEnd('s'), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double seconds))
                        retryAfter = TimeSpan.FromSeconds(seconds);
                }
            }
        }
        catch (Exception)
        {
            // Ayrıntı yoksa aşağıdaki varsayılanlar kullanılır.
        }

        // Aşılan kotalardan biri günlükse günlük say; hiç ayrıntı yoksa mesaja bak.
        bool daily = quotaIds.Count > 0
            ? quotaIds.Any(id => id.Contains("PerDay", StringComparison.OrdinalIgnoreCase))
            : apiMessage?.Contains("per day", StringComparison.OrdinalIgnoreCase) == true;

        string detailText = quotaIds.Count > 0 ? string.Join(", ", quotaIds.Distinct()) : apiMessage ?? "ayrıntı yok";
        return new QuotaExceededForModelException(daily, retryAfter, detailText);
    }

    // --- Sadece bu sınıfın içinde kullanılan, "başka modeli dene / bekle" anlamına gelen hatalar ---
    private sealed class ModelNotFoundException : Exception;
    private sealed class ServiceUnavailableException(int statusCode) : Exception
    {
        public int StatusCode { get; } = statusCode;
    }

    private sealed class QuotaExceededForModelException(bool isDaily, TimeSpan retryAfter, string detail) : Exception
    {
        public bool IsDaily { get; } = isDaily;
        public TimeSpan RetryAfter { get; } = retryAfter;
        public string Detail { get; } = detail;
    }

    // --- Gemini JSON yanıtlarının C# karşılıkları ---

    private sealed class GenerateResponse
    {
        [JsonPropertyName("candidates")] public List<Candidate>? Candidates { get; set; }
        [JsonPropertyName("promptFeedback")] public PromptFeedback? PromptFeedback { get; set; }
    }

    private sealed class Candidate
    {
        [JsonPropertyName("content")] public Content? Content { get; set; }
        [JsonPropertyName("finishReason")] public string? FinishReason { get; set; }
    }

    private sealed class Content
    {
        [JsonPropertyName("parts")] public List<Part>? Parts { get; set; }
    }

    private sealed class Part
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("thought")] public bool? Thought { get; set; }
    }

    private sealed class PromptFeedback
    {
        [JsonPropertyName("blockReason")] public string? BlockReason { get; set; }
    }

    private sealed class ModelList
    {
        [JsonPropertyName("models")] public List<ModelInfo>? Models { get; set; }
    }

    private sealed class ModelInfo
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("supportedGenerationMethods")] public List<string>? Methods { get; set; }
    }
}

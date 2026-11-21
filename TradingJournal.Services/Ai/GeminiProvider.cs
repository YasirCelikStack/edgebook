using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingJournal.Services.Ai;

// Google Gemini REST API istemcisi (generateContent uç noktası).
// Model adları sık değiştiği için: ayarlanan model bulunamazsa (404) hesabın erişebildiği
// modeller listelenir ve en uygun "Flash" modeli otomatik seçilir.
public class GeminiProvider : IAiProvider
{
    public const string DefaultModel = "gemini-flash-latest";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";

    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(90) };

    // Otomatik bulunan model, uygulama açık kaldıkça tekrar aranmasın.
    private static string? _resolvedModel;

    private readonly string _apiKey;
    private readonly string _model;

    public GeminiProvider(string? apiKey, string? model = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AiAuthenticationException("Gemini API anahtarı girilmemiş. Koç sayfasındaki \"Anahtar ayarla\" düğmesini kullan.");
        _apiKey = apiKey.Trim();
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
    }

    public string Name => $"Google Gemini ({_resolvedModel ?? _model})";

    public async Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        string model = _resolvedModel ?? _model;
        try
        {
            return await SendAsync(model, systemPrompt, messages, cancellationToken);
        }
        catch (ModelNotFoundException)
        {
            string fallback = await FindFlashModelAsync(cancellationToken)
                              ?? throw new AiException($"\"{model}\" modeli bulunamadı ve kullanılabilir bir Flash modeli yok.");
            _resolvedModel = fallback;
            return await SendAsync(fallback, systemPrompt, messages, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AiException("Gemini'ye bağlanılamadı. İnternet bağlantını kontrol et.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiException("Gemini zamanında yanıt vermedi. Biraz sonra tekrar dene.", ex);
        }
    }

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
            generationConfig = new { temperature = 0.4 }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/models/{model}:generateContent")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("x-goog-api-key", _apiKey);

        using HttpResponseMessage response = await SharedHttp.SendAsync(request, ct);
        string json = await response.Content.ReadAsStringAsync(ct);

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
            throw new AiException("Gemini boş yanıt döndürdü. Tekrar dene.");
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
            HttpStatusCode.TooManyRequests => new AiQuotaExceededException(),
            HttpStatusCode.NotFound => new ModelNotFoundException(),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new AiAuthenticationException("Gemini anahtarı yetkisiz. Anahtarı AI Studio'dan kontrol et."),
            HttpStatusCode.BadRequest when apiMessage?.Contains("API key", StringComparison.OrdinalIgnoreCase) == true =>
                new AiAuthenticationException("Gemini anahtarı geçersiz. AI Studio'dan kopyaladığın anahtarı tekrar gir."),
            >= HttpStatusCode.InternalServerError =>
                new AiException("Gemini şu an yoğun ya da erişilemiyor. Biraz sonra tekrar dene."),
            _ => new AiException($"Gemini hatası ({(int)status}): {apiMessage ?? "bilinmeyen hata"}")
        };
    }

    // Hesabın erişebildiği modellerden metin üretebilen, en güncel "flash" modelini seçer.
    private async Task<string?> FindFlashModelAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/models?pageSize=200");
        request.Headers.Add("x-goog-api-key", _apiKey);
        using HttpResponseMessage response = await SharedHttp.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        ModelList? list = await response.Content.ReadFromJsonAsync<ModelList>(ct);
        string[] excluded = ["lite", "image", "tts", "audio", "live", "embedding", "vision"];

        var candidates = (list?.Models ?? [])
            .Where(m => m.Methods?.Contains("generateContent") == true)
            .Select(m => m.Name.Replace("models/", ""))
            .Where(n => n.Contains("flash") && !excluded.Any(word => n.Contains(word)))
            .ToList();

        // Önce kararlı sürümler, sonra önizlemeler; kendi içinde en yeni sürüm önce.
        return candidates
            .OrderBy(n => n.Contains("preview") || n.Contains("exp"))
            .ThenByDescending(n => n, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private sealed class ModelNotFoundException : Exception;

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

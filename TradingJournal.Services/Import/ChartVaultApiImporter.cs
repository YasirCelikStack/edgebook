using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TradingJournal.Core.Enums;

namespace TradingJournal.Services.Import;

// Chart Vault'un Journal kayıtlarını Supabase REST API'sinden çeker.
//   1) /auth/v1/token ile e-posta + şifreyle giriş yapılır, kısa ömürlü erişim anahtarı (JWT) alınır.
//   2) /rest/v1/journal_entries ile kayıtlar okunur. Tablodaki RLS kuralı sayesinde
//      sadece giriş yapan kullanıcının kendi kayıtları döner.
// Sadece OKUMA yapılır; Chart Vault'taki hiçbir kayıt değiştirilmez.
public class ChartVaultApiImporter : IJournalImporter
{
    // Hiç girilmemiş (beklemede / gerçekleşmedi) planlar aktarılmaz.
    private const string TakenStatuses = "open,tp,sl,be,manual";

    private readonly ChartVaultSettings _settings;
    private readonly string _email;
    private readonly string _password;
    private readonly HttpClient _http;

    public ChartVaultApiImporter(ChartVaultSettings settings, string email, string password, HttpClient? http = null)
    {
        if (!settings.IsConfigured)
            throw new ImportException("Chart Vault bağlantı ayarları eksik (appsettings.Local.json).");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            throw new ImportException("Chart Vault e-posta ve şifrenizi girin.");

        _settings = settings;
        _email = email.Trim();
        _password = password;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public string SourceName => "Chart Vault";

    private string BaseUrl => _settings.SupabaseUrl.Trim().TrimEnd('/');

    public async Task<IReadOnlyList<ImportedTrade>> FetchAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string token = await SignInAsync(cancellationToken);
            List<JournalEntryDto> entries = await GetEntriesAsync(token, cancellationToken);
            return entries.Select(Map).Where(t => t is not null).Select(t => t!).ToList();
        }
        catch (HttpRequestException ex)
        {
            throw new ImportException("Chart Vault'a bağlanılamadı. İnternet bağlantısını ve Supabase adresini kontrol edin.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ImportException("Chart Vault yanıt vermedi (zaman aşımı).", ex);
        }
    }

    private async Task<string> SignInAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/auth/v1/token?grant_type=password")
        {
            Content = JsonContent.Create(new { email = _email, password = _password })
        };
        request.Headers.Add("apikey", _settings.PublishableKey.Trim());

        using HttpResponseMessage response = await _http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            throw new ImportException("Giriş başarısız: e-posta veya şifre hatalı.");
        if (!response.IsSuccessStatusCode)
            throw new ImportException($"Giriş başarısız ({(int)response.StatusCode}). Supabase adresini ve key'i kontrol edin.");

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
        return body?.AccessToken ?? throw new ImportException("Giriş yanıtında erişim anahtarı yok.");
    }

    private async Task<List<JournalEntryDto>> GetEntriesAsync(string token, CancellationToken ct)
    {
        string url = $"{BaseUrl}/rest/v1/journal_entries?select=*&status=in.({TakenStatuses})&order=entry_time.asc&limit=5000";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("apikey", _settings.PublishableKey.Trim());
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new ImportException("Chart Vault'ta journal_entries tablosu bulunamadı.");
        if (!response.IsSuccessStatusCode)
            throw new ImportException($"Journal kayıtları okunamadı ({(int)response.StatusCode}).");

        return await response.Content.ReadFromJsonAsync<List<JournalEntryDto>>(ct) ?? new();
    }

    // Chart Vault kaydını ortak ImportedTrade yapısına çevirir. Eksik/bozuk kayıtlar atlanır (null).
    private static ImportedTrade? Map(JournalEntryDto e)
    {
        if (e.Entry <= 0 || e.StopLoss <= 0 || e.Entry == e.StopLoss)
            return null;

        bool closed = e.Status is "tp" or "sl" or "be" or "manual";
        DateTime openTime = (e.EntryHitAt ?? e.EntryTime).ToLocalTime();
        decimal? exitPrice = e.ExitPrice is double x ? (decimal)x : null;
        if (e.Status == "be") exitPrice ??= (decimal)e.Entry;

        return new ImportedTrade
        {
            ExternalId = $"chartvault:{e.Id}",
            Symbol = e.Symbol.Trim().ToUpperInvariant(),
            Timeframe = e.Timeframe,
            Direction = e.Side == "short" ? TradeDirection.Short : TradeDirection.Long,
            EntryPrice = (decimal)e.Entry,
            StopLoss = (decimal)e.StopLoss,
            TakeProfit = e.TakeProfit > 0 ? (decimal)e.TakeProfit : null,
            OpenTime = openTime,
            IsClosed = closed,
            CloseTime = closed ? (e.ExitAt ?? e.EntryHitAt ?? e.EntryTime).ToLocalTime() : null,
            ExitPrice = closed ? exitPrice : null,
            ResultR = e.ResultR is double r ? (decimal)r : null,
            Setup = string.IsNullOrWhiteSpace(e.Setup) ? null : e.Setup.Trim(),
            Note = e.Note,
            SourceStatus = e.Status switch
            {
                "tp" => "TP",
                "sl" => "SL",
                "be" => "Başa baş",
                "manual" => "Elle kapatıldı",
                _ => "Açık"
            }
        };
    }

    // --- Supabase JSON yanıtlarının C# karşılıkları ---

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    }

    private sealed class JournalEntryDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("symbol")] public string Symbol { get; set; } = string.Empty;
        [JsonPropertyName("tf")] public string? Timeframe { get; set; }
        [JsonPropertyName("side")] public string Side { get; set; } = "long";
        [JsonPropertyName("entry")] public double Entry { get; set; }
        [JsonPropertyName("sl")] public double StopLoss { get; set; }
        [JsonPropertyName("tp")] public double TakeProfit { get; set; }
        [JsonPropertyName("entry_time")] public DateTime EntryTime { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
        [JsonPropertyName("result_r")] public double? ResultR { get; set; }
        [JsonPropertyName("entry_hit_at")] public DateTime? EntryHitAt { get; set; }
        [JsonPropertyName("exit_at")] public DateTime? ExitAt { get; set; }
        [JsonPropertyName("exit_price")] public double? ExitPrice { get; set; }
        [JsonPropertyName("setup")] public string? Setup { get; set; }
        [JsonPropertyName("note")] public string? Note { get; set; }
    }
}

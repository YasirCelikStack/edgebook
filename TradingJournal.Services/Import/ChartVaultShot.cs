using System.Text.Json.Serialization;

namespace TradingJournal.Services.Import;

// Chart Vault'un /api/journal/shots uç noktasının döndürdüğü tek görsel.
// Adresler kısa süreli (1 saat) imzalı linklerdir; hemen indirilip bilgisayara kaydedilir.
public sealed class ChartVaultShot
{
    [JsonPropertyName("journal_id")] public string JournalId { get; set; } = string.Empty;
    [JsonPropertyName("chart_id")] public string ChartId { get; set; } = string.Empty;
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    [JsonPropertyName("thumb_url")] public string? ThumbUrl { get; set; }
    [JsonPropertyName("content_type")] public string? ContentType { get; set; }
    [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }

    // Aynı görselin ikinci kez indirilmemesi için ekran görüntüsünün açıklamasına yazılan işaret.
    public string Marker => $"chartvault:{ChartId}";
}

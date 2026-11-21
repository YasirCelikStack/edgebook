using TradingJournal.Core.Enums;

namespace TradingJournal.Services.Import;

// Dış kaynaktan (Chart Vault, ileride CSV vb.) gelen işlemin kaynaktan bağımsız hali.
// Her içe aktarıcı kendi formatını bu ortak yapıya çevirir; kaydetme işi tek yerde yapılır.
public record ImportedTrade
{
    public required string ExternalId { get; init; }
    public required string Symbol { get; init; }
    public string? Timeframe { get; init; }
    public required TradeDirection Direction { get; init; }
    public required decimal EntryPrice { get; init; }
    public required decimal StopLoss { get; init; }
    public decimal? TakeProfit { get; init; }
    public required DateTime OpenTime { get; init; }

    // Kapanmamışsa null.
    public DateTime? CloseTime { get; init; }
    public decimal? ExitPrice { get; init; }

    // Kaynaktaki sonuç (R cinsinden), çıkış fiyatı yoksa hesaplamak için.
    public decimal? ResultR { get; init; }
    public bool IsClosed { get; init; }

    public string? Setup { get; init; }
    public string? Note { get; init; }

    // Önizlemede gösterilen kaynak durumu ("TP", "SL", "Açık"...).
    public string SourceStatus { get; init; } = string.Empty;
}

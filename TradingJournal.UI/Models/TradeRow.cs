using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.Services.Statistics;

namespace TradingJournal.UI.Models;

// İşlem listesinde (GridControl) bir satır. Trade nesnesini grid'in kolayca
// gösterebileceği düz alanlara çevirir: hesap adı, sembol, etiketler tek metin vb.
// Grid'e entity'nin kendisini vermek yerine bunu vermek, arayüzü veritabanı
// modelinden ayırır (ViewModel / DTO yaklaşımı).
public class TradeRow
{
    public int Id { get; init; }
    public DateTime OpenTime { get; init; }
    public DateTime? CloseTime { get; init; }
    public string Account { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Market { get; init; } = string.Empty;
    public TradeDirection Direction { get; init; }
    public string Session { get; init; } = string.Empty;
    public string? Strategy { get; init; }
    public decimal EntryPrice { get; init; }
    public decimal StopLoss { get; init; }
    public decimal? TakeProfit { get; init; }
    public decimal? ExitPrice { get; init; }
    public string Size { get; init; } = string.Empty;
    public decimal Risk { get; init; }
    public decimal? NetPnL { get; init; }
    public decimal? RMultiple { get; init; }
    public bool IsClosed { get; init; }
    public string Status => IsClosed ? "Kapalı" : "Açık";
    public string Tags { get; init; } = string.Empty;
    public int? Rating { get; init; }
    public int ScreenshotCount { get; set; }

    // İlk ekran görüntüsünün tam dosya yolu (yoksa null). Listede küçük resim olarak gösterilir.
    public string? ThumbnailPath { get; set; }
    public string DirectionText => Direction == TradeDirection.Long ? "▲ Long" : "▼ Short";
    public string Stars => Rating is int r ? new string('★', r) : string.Empty;

    public static TradeRow From(Trade trade) => new()
    {
        Id = trade.Id,
        OpenTime = trade.OpenTime,
        CloseTime = trade.CloseTime,
        Account = trade.Account?.Name ?? string.Empty,
        Symbol = trade.Instrument?.Symbol ?? string.Empty,
        Direction = trade.Direction,
        Session = SessionClassifier.GetSession(trade.OpenTime).ToTurkish(),
        Strategy = trade.Strategy?.Name,
        EntryPrice = trade.EntryPrice,
        StopLoss = trade.StopLoss,
        TakeProfit = trade.TakeProfit,
        ExitPrice = trade.ExitPrice,
        Risk = Math.Round(trade.CalculateRiskAmount(), 2),
        NetPnL = trade.NetPnL is decimal pnl ? Math.Round(pnl, 2) : null,
        RMultiple = trade.RMultiple is decimal r ? Math.Round(r, 2) : null,
        IsClosed = trade.IsClosed,
        Tags = string.Join(", ", trade.Tags.Select(t => t.Name).OrderBy(n => n)),
        Rating = trade.Rating,

        // Çok biçimlilik: işlemin gerçek türüne göre farklı bilgi gösterilir.
        Market = trade switch
        {
            ForexTrade => "Forex",
            CryptoTrade => "Kripto",
            _ => "?"
        },
        Size = trade switch
        {
            ForexTrade f => $"{f.LotSize:0.##} lot",
            CryptoTrade c => $"{c.Quantity:0.####} (x{c.Leverage})",
            _ => string.Empty
        }
    };
}

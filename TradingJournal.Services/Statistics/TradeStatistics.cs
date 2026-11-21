namespace TradingJournal.Services.Statistics;

// Bir işlem grubunun özet performansı. Sadece okunabilir (init) property'ler:
// hesaplandıktan sonra dışarıdan değiştirilemez.
public class TradeStatistics
{
    public int TotalTrades { get; init; }
    public int Wins { get; init; }
    public int Losses { get; init; }

    // 0-100 arası yüzde.
    public decimal WinRate => TotalTrades == 0 ? 0 : Math.Round(100m * Wins / TotalTrades, 2);

    public decimal NetProfit { get; init; }
    public decimal GrossProfit { get; init; }
    public decimal GrossLoss { get; init; }          // Negatif değer (örn. -1.250)

    // Kazançların kayıplara oranı. Hiç kayıp yoksa tanımsız (null).
    public decimal? ProfitFactor => GrossLoss == 0 ? null : Math.Round(GrossProfit / Math.Abs(GrossLoss), 2);

    public decimal AverageWin { get; init; }
    public decimal AverageLoss { get; init; }        // Negatif değer
    public decimal? AverageR { get; init; }

    // İşlem başına ortalama beklenen kazanç.
    public decimal Expectancy => TotalTrades == 0 ? 0 : Math.Round(NetProfit / TotalTrades, 2);

    public decimal LargestWin { get; init; }
    public decimal LargestLoss { get; init; }
    public decimal MaxDrawdown { get; init; }        // Zirveden en büyük düşüş (pozitif tutar)

    public static TradeStatistics Empty { get; } = new();
}

// Gruplanmış istatistik satırı: "Cuma", "London", "EURUSD" gibi bir anahtar ve o grubun özeti.
public record GroupStatistics<TKey>(TKey Key, TradeStatistics Stats);

// Equity curve'deki tek nokta: o işlem kapandıktan sonraki bakiye.
public record EquityPoint(DateTime Time, decimal Balance, decimal NetPnL);

namespace TradingJournal.Core.Entities;

public class ForexTrade : Trade
{
    private decimal _lotSize;

    public decimal LotSize
    {
        get => _lotSize;
        set
        {
            if (value <= 0)
                throw new ArgumentException("Lot büyüklüğü 0'dan büyük olmalıdır.");
            _lotSize = value;
        }
    }

    // EURUSD için 0.0001, USDJPY için 0.01. Enstrümana göre değişir.
    public decimal PipSize { get; set; } = 0.0001m;

    // 1 standart lotta 1 pip'in dolar değeri (çoğu USD paritesinde 10$).
    public decimal PipValuePerLot { get; set; } = 10m;

    private decimal ToPips(decimal priceDifference) => priceDifference / PipSize;

    protected override decimal CalculateGrossPnL()
    {
        decimal pips = ToPips((ExitPrice!.Value - EntryPrice) * DirectionSign);
        return pips * PipValuePerLot * LotSize;
    }

    public override decimal CalculateRiskAmount()
    {
        decimal stopPips = ToPips(Math.Abs(EntryPrice - StopLoss));
        return stopPips * PipValuePerLot * LotSize;
    }
}
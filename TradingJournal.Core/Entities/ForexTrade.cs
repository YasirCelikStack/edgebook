using TradingJournal.Core.Exceptions;

namespace TradingJournal.Core.Entities;

public class ForexTrade : Trade
{
    private decimal _lotSize;
    private decimal _pipSize = 0.0001m;
    private decimal _pipValuePerLot = 10m;

    public decimal LotSize
    {
        get => _lotSize;
        set => _lotSize = Guard.Positive(value, "Lot büyüklüğü");
    }

    // EURUSD için 0.0001, USDJPY için 0.01. İşlem açılırken enstrümandan kopyalanır;
    // böylece enstrüman ayarı sonradan değişse bile eski işlemlerin sonucu bozulmaz.
    public decimal PipSize
    {
        get => _pipSize;
        set => _pipSize = Guard.Positive(value, "Pip büyüklüğü");
    }

    // 1 standart lotta 1 pip'in dolar değeri (çoğu USD paritesinde 10$).
    public decimal PipValuePerLot
    {
        get => _pipValuePerLot;
        set => _pipValuePerLot = Guard.Positive(value, "Pip değeri");
    }

    private decimal ToPips(decimal priceDifference) => priceDifference / PipSize;

    public decimal? ResultInPips => IsClosed ? ToPips((ExitPrice!.Value - EntryPrice) * DirectionSign) : null;

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

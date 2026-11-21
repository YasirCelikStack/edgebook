namespace TradingJournal.Core.Entities;

public class CryptoTrade : Trade
{
    private decimal _quantity;
    private int _leverage = 1;

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (value <= 0)
                throw new ArgumentException("Miktar 0'dan büyük olmalıdır.");
            _quantity = value;
        }
    }

    public int Leverage
    {
        get => _leverage;
        set
        {
            if (value < 1 || value > 125)
                throw new ArgumentException("Kaldıraç 1 ile 125 arasında olmalıdır.");
            _leverage = value;
        }
    }

    // Pozisyon için bağlanan teminat (kâr/zararı değiştirmez).
    public decimal Margin => EntryPrice * Quantity / Leverage;

    protected override decimal CalculateGrossPnL()
        => (ExitPrice!.Value - EntryPrice) * DirectionSign * Quantity;

    public override decimal CalculateRiskAmount()
        => Math.Abs(EntryPrice - StopLoss) * Quantity;
}
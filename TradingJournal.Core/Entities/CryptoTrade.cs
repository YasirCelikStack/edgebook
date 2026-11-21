using TradingJournal.Core.Exceptions;

namespace TradingJournal.Core.Entities;

public class CryptoTrade : Trade
{
    private decimal _quantity;
    private int _leverage = 1;

    public decimal Quantity
    {
        get => _quantity;
        set => _quantity = Guard.Positive(value, "Miktar");
    }

    public int Leverage
    {
        get => _leverage;
        set => _leverage = Guard.InRange(value, 1, 125, "Kaldıraç");
    }

    // Pozisyon için bağlanan teminat (kâr/zararı değiştirmez).
    public decimal Margin => EntryPrice * Quantity / Leverage;

    protected override decimal CalculateGrossPnL()
        => (ExitPrice!.Value - EntryPrice) * DirectionSign * Quantity;

    public override decimal CalculateRiskAmount()
        => Math.Abs(EntryPrice - StopLoss) * Quantity;
}

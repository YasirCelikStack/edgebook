using TradingJournal.Core.Enums;
using TradingJournal.Core.Exceptions;

namespace TradingJournal.Core.Entities;

// İşlem yapılan parite / coin: EURUSD, XAUUSD, BTCUSDT gibi.
public class Instrument : BaseEntity
{
    private string _symbol = string.Empty;
    private decimal _pipSize = 0.0001m;
    private decimal _pipValuePerLot = 10m;

    public string Symbol
    {
        get => _symbol;
        set => _symbol = Guard.NotEmpty(value, "Sembol", maxLength: 20).ToUpperInvariant();
    }

    public InstrumentType Type { get; set; } = InstrumentType.Forex;

    // Forex işlemleri açılırken varsayılan değer olarak bu iki alan kullanılır.
    public decimal PipSize
    {
        get => _pipSize;
        set => _pipSize = Guard.Positive(value, "Pip büyüklüğü");
    }

    public decimal PipValuePerLot
    {
        get => _pipValuePerLot;
        set => _pipValuePerLot = Guard.Positive(value, "Pip değeri");
    }

    public ICollection<Trade> Trades { get; set; } = new List<Trade>();

    public override string ToString() => Symbol;
}

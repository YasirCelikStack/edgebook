using TradingJournal.Core.Exceptions;

namespace TradingJournal.Core.Entities;

// Trader'ın kullandığı setup: "London Sweep + FVG", "Breaker Block" gibi.
public class Strategy : BaseEntity
{
    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set => _name = Guard.NotEmpty(value, "Strateji adı");
    }

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Trade> Trades { get; set; } = new List<Trade>();

    public override string ToString() => Name;
}

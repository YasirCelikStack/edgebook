using TradingJournal.Core.Exceptions;

namespace TradingJournal.Core.Entities;

// Bir stratejinin kontrol listesindeki tek madde: "HTF bias ile aynı yönde", "Killzone içinde" gibi.
// İşlem girilirken her madde için "uyuldu / uyulmadı" işaretlenir (TradeRuleCheck).
public class StrategyRule : BaseEntity
{
    private string _text = string.Empty;

    public int StrategyId { get; set; }
    public Strategy Strategy { get; set; } = null!;

    public string Text
    {
        get => _text;
        set => _text = Guard.NotEmpty(value, "Kural", maxLength: 200).Trim();
    }

    // Kontrol listesindeki sırası (0'dan başlar).
    public int SortOrder { get; set; }

    public ICollection<TradeRuleCheck> Checks { get; set; } = new List<TradeRuleCheck>();

    public override string ToString() => Text;
}

namespace TradingJournal.Core.Entities;

// Bir işlemde, stratejinin bir kuralına uyulup uyulmadığı.
// İşlem ile strateji kuralı arasındaki çoka-çok ilişkinin, ek bilgi (IsMet) taşıyan ara tablosu.
// Anahtarı (TradeId, StrategyRuleId) ikilisidir; bu yüzden BaseEntity'den türemez.
public class TradeRuleCheck
{
    public int TradeId { get; set; }
    public Trade Trade { get; set; } = null!;

    public int StrategyRuleId { get; set; }
    public StrategyRule Rule { get; set; } = null!;

    public bool IsMet { get; set; }
}

namespace TradingJournal.Core.Enums;

// Prop-firm hesaplarında sık görülen kural türleri.
public enum RiskRuleType
{
    DailyLoss = 1,     // Bir günde kaybedilebilecek en fazla tutar (% başlangıç bakiyesi)
    MaxDrawdown = 2    // Başlangıç bakiyesinden toplam en fazla düşüş (%)
}

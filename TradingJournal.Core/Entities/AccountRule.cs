using TradingJournal.Core.Enums;
using TradingJournal.Core.Exceptions;

namespace TradingJournal.Core.Entities;

// Bir hesaba bağlı risk kuralının veritabanındaki kaydı.
// Kuralın nasıl kontrol edileceği Services katmanındaki IRiskRule sınıflarında olacak.
public class AccountRule : BaseEntity
{
    private decimal _limitPercent;

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public RiskRuleType RuleType { get; set; }

    // Örn. 5 => günlük %5 kayıp sınırı.
    public decimal LimitPercent
    {
        get => _limitPercent;
        set
        {
            if (value <= 0 || value > 100)
                throw new DomainException("Kural limiti 0 ile 100 arasında olmalıdır.");
            _limitPercent = value;
        }
    }

    public bool IsActive { get; set; } = true;

    // Yüzdeyi hesabın para birimine çevirir: 100.000$ hesapta %5 => 5.000$.
    public decimal LimitAmount => Account is null ? 0 : Account.InitialBalance * LimitPercent / 100m;
}

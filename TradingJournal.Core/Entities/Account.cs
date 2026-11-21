using TradingJournal.Core.Enums;
using TradingJournal.Core.Exceptions;

namespace TradingJournal.Core.Entities;

// Trader'ın işlem yaptığı hesap: demo, gerçek veya prop-firm challenge hesabı.
public class Account : BaseEntity
{
    private string _name = string.Empty;
    private string _currency = "USD";
    private decimal _initialBalance;

    public string Name
    {
        get => _name;
        set => _name = Guard.NotEmpty(value, "Hesap adı");
    }

    public string? Broker { get; set; }
    public AccountType Type { get; set; } = AccountType.Demo;

    public string Currency
    {
        get => _currency;
        set => _currency = Guard.NotEmpty(value, "Para birimi", maxLength: 3).ToUpperInvariant();
    }

    public decimal InitialBalance
    {
        get => _initialBalance;
        set => _initialBalance = Guard.Positive(value, "Başlangıç bakiyesi");
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Bire-çok ilişkiler: bir hesabın birden fazla işlemi ve kuralı olur.
    public ICollection<Trade> Trades { get; set; } = new List<Trade>();
    public ICollection<AccountRule> Rules { get; set; } = new List<AccountRule>();

    public override string ToString() => $"{Name} ({Type})";
}

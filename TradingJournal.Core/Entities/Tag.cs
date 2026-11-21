using TradingJournal.Core.Exceptions;

namespace TradingJournal.Core.Entities;

// İşlemlere eklenen etiket: "FOMO", "Revenge", "A+ Setup", "Haber günü" gibi.
// Bir işlemin birden çok etiketi, bir etiketin birden çok işlemi olabilir (çoka-çok).
public class Tag : BaseEntity
{
    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set => _name = Guard.NotEmpty(value, "Etiket adı", maxLength: 40);
    }

    // Arayüzde etiketi renkli göstermek için (#RRGGBB).
    public string? ColorHex { get; set; }

    public ICollection<Trade> Trades { get; set; } = new List<Trade>();

    public override string ToString() => Name;
}

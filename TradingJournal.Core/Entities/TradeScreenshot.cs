using TradingJournal.Core.Exceptions;

namespace TradingJournal.Core.Entities;

// Bir işleme ait grafik ekran görüntüsü. Resmin kendisi diskte,
// veritabanında sadece dosya yolu tutulur (veritabanı şişmesin diye).
public class TradeScreenshot : BaseEntity
{
    private string _filePath = string.Empty;

    public int TradeId { get; set; }
    public Trade Trade { get; set; } = null!;

    public string FilePath
    {
        get => _filePath;
        set => _filePath = Guard.NotEmpty(value, "Dosya yolu", maxLength: 500);
    }

    public string? Caption { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

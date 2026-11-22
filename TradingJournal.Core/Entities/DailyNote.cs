using TradingJournal.Core.Exceptions;

namespace TradingJournal.Core.Entities;

// Güne ait not: seans öncesi plan ve gün sonu değerlendirmesi. İşlemlerden bağımsızdır
// (işlem açılmayan günün de planı/değerlendirmesi olabilir). Her gün için en fazla bir kayıt.
public class DailyNote : BaseEntity
{
    private DateTime _date;
    private int? _mood;

    // Sadece tarih kısmı tutulur (saat 00:00).
    public DateTime Date
    {
        get => _date;
        set => _date = value.Date;
    }

    public string? Plan { get; set; }
    public string? Review { get; set; }

    // Günün duygu durumu / zihinsel hazırlık: 1 (çok kötü) - 5 (çok iyi).
    public int? Mood
    {
        get => _mood;
        set => _mood = value.HasValue ? Guard.InRange(value.Value, 1, 5, "Duygu durumu") : null;
    }

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public bool IsEmpty => string.IsNullOrWhiteSpace(Plan) && string.IsNullOrWhiteSpace(Review) && Mood is null;
}

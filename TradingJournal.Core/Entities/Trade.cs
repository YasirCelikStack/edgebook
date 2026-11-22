using TradingJournal.Core.Enums;
using TradingJournal.Core.Exceptions;

namespace TradingJournal.Core.Entities;

public abstract class Trade : BaseEntity
{
    // Kapsülleme: alanlar private, dışarıya doğrulamalı property ile açılıyor.
    private decimal _entryPrice;
    private decimal _stopLoss;
    private decimal? _exitPrice;
    private decimal _commission;
    private int? _rating;

    // --- İlişkiler (foreign key + navigation property) ---
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public int InstrumentId { get; set; }
    public Instrument Instrument { get; set; } = null!;

    // Strateji seçmek zorunlu değil, bu yüzden nullable.
    public int? StrategyId { get; set; }
    public Strategy? Strategy { get; set; }

    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public ICollection<TradeScreenshot> Screenshots { get; set; } = new List<TradeScreenshot>();

    // Strateji kontrol listesinin bu işlemdeki cevapları.
    public ICollection<TradeRuleCheck> RuleChecks { get; set; } = new List<TradeRuleCheck>();

    // --- İşlem bilgileri ---
    public TradeDirection Direction { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public string? Notes { get; set; }

    // Başka bir sistemden içe aktarıldıysa oradaki kimliği (örn. "chartvault:3f2a...").
    // Aynı işlemin ikinci kez aktarılmasını engellemek için kullanılır.
    public string? ExternalId { get; set; }

    public decimal EntryPrice
    {
        get => _entryPrice;
        set => _entryPrice = Guard.Positive(value, "Giriş fiyatı");
    }

    public decimal StopLoss
    {
        get => _stopLoss;
        set => _stopLoss = Guard.Positive(value, "Stop loss");
    }

    public decimal? TakeProfit { get; set; }

    public decimal? ExitPrice
    {
        get => _exitPrice;
        set => _exitPrice = value.HasValue ? Guard.Positive(value.Value, "Çıkış fiyatı") : null;
    }

    public decimal Commission
    {
        get => _commission;
        set => _commission = Guard.NotNegative(value, "Komisyon");
    }

    // İşlemin uygulamasına (execution) verilen not: 1-5 yıldız.
    public int? Rating
    {
        get => _rating;
        set => _rating = value.HasValue ? Guard.InRange(value.Value, 1, 5, "Puan") : null;
    }

    // --- Davranışlar ---

    // İşlemi kapatmanın tek doğru yolu: çıkış fiyatı ve zamanını birlikte, kontrollü şekilde atar.
    public void Close(decimal exitPrice, DateTime closeTime)
    {
        if (IsClosed)
            throw new DomainException("Bu işlem zaten kapatılmış.");
        if (closeTime < OpenTime)
            throw new DomainException("Kapanış zamanı açılış zamanından önce olamaz.");

        ExitPrice = exitPrice;
        CloseTime = closeTime;
    }

    // --- Hesaplanan değerler: veritabanına yazılmaz, her seferinde hesaplanır ---
    public bool IsClosed => ExitPrice.HasValue;

    // Long için +1, Short için -1: kâr formülünü tek satırda yazmamızı sağlar.
    protected int DirectionSign => Direction == TradeDirection.Long ? 1 : -1;

    // Alt sınıflar bunları KENDİ yöntemleriyle hesaplamak ZORUNDA.
    protected abstract decimal CalculateGrossPnL();
    public abstract decimal CalculateRiskAmount();

    public decimal? NetPnL => IsClosed ? CalculateGrossPnL() - Commission : null;

    // R-multiple: kazancın, göze alınan riske oranı (2R = riskin 2 katı kazanç).
    public decimal? RMultiple
    {
        get
        {
            decimal risk = CalculateRiskAmount();
            if (!IsClosed || risk == 0) return null;
            return NetPnL / risk;
        }
    }

    public bool IsWin => NetPnL > 0;

    // Kurala uyum: kontrol listesi doldurulmadıysa NotTracked, hepsine uyulduysa Followed, biri bile çiğnendiyse Broken.
    public RuleCompliance Compliance => RuleChecks.Count == 0
        ? RuleCompliance.NotTracked
        : RuleChecks.All(c => c.IsMet) ? RuleCompliance.Followed : RuleCompliance.Broken;
}

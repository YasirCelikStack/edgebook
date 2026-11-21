using TradingJournal.Core.Enums;

namespace TradingJournal.Core.Entities;

public abstract class Trade : BaseEntity
{
    // Kapsülleme: alanlar private, dışarıya doğrulamalı property ile açılıyor.
    private decimal _entryPrice;
    private decimal _stopLoss;
    private decimal? _exitPrice;
    private decimal _commission;

    public int AccountId { get; set; }
    public TradeDirection Direction { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public string? Notes { get; set; }

    public decimal EntryPrice
    {
        get => _entryPrice;
        set
        {
            if (value <= 0)
                throw new ArgumentException("Giriş fiyatı 0'dan büyük olmalıdır.");
            _entryPrice = value;
        }
    }

    public decimal StopLoss
    {
        get => _stopLoss;
        set
        {
            if (value <= 0)
                throw new ArgumentException("Stop loss 0'dan büyük olmalıdır.");
            _stopLoss = value;
        }
    }

    public decimal? ExitPrice
    {
        get => _exitPrice;
        set
        {
            if (value is <= 0)
                throw new ArgumentException("Çıkış fiyatı 0'dan büyük olmalıdır.");
            _exitPrice = value;
        }
    }

    public decimal Commission
    {
        get => _commission;
        set
        {
            if (value < 0)
                throw new ArgumentException("Komisyon negatif olamaz.");
            _commission = value;
        }
    }

    // Hesaplanan değerler: veritabanına yazılmaz, her seferinde hesaplanır.
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
}
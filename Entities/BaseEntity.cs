namespace TradingJournal.Core.Entities;

// Tüm tabloların ortak atası: her kaydın bir Id'si olur.
public abstract class BaseEntity
{
    public int Id { get; set; }
}
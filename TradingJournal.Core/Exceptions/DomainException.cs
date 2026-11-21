namespace TradingJournal.Core.Exceptions;

// Kendi istisna sınıfımız: iş kuralı ihlallerini (geçersiz fiyat, boş isim vb.)
// diğer hatalardan ayırır. Arayüz bu türü yakalayıp kullanıcıya anlamlı mesaj gösterir.
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}

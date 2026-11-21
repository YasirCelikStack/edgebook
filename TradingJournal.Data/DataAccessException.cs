namespace TradingJournal.Data;

// Veritabanı hatalarını (kilitli dosya, ilişki ihlali vb.) kullanıcıya gösterilebilecek
// anlaşılır bir mesaja çevirir. Asıl hata InnerException içinde saklanır.
public class DataAccessException : Exception
{
    public DataAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

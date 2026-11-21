namespace TradingJournal.Services.Ai;

// Yapay zeka hatalarının ortak atası: arayüz bu türü yakalayıp mesajı kullanıcıya gösterir.
public class AiException : Exception
{
    public AiException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

// Günlük / dakikalık ücretsiz kota doldu (HTTP 429).
public class AiQuotaExceededException : AiException
{
    public AiQuotaExceededException()
        : base("Yapay zeka kullanım hakkı şimdilik doldu. Dakikalık sınırsa biraz bekleyip tekrar dene; " +
               "günlük sınırsa kota Türkiye saatiyle sabah 10-11 civarında yenilenir.")
    {
    }
}

// Anahtar yok, geçersiz ya da yetkisiz (HTTP 400/401/403).
public class AiAuthenticationException : AiException
{
    public AiAuthenticationException(string message) : base(message)
    {
    }
}

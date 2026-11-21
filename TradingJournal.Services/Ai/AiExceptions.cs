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
    public AiQuotaExceededException(bool daily, string? detail = null)
        : base((daily
                   ? "Bugünkü ücretsiz yapay zeka hakkın doldu. Kota Türkiye saatiyle sabah 10-11 civarında yenilenir."
                   : "Dakikalık istek sınırına takıldın (ücretsiz katmanda dakikada birkaç istek). 1 dakika bekleyip tekrar dene.")
               + (detail is null ? "" : $"\n(Gemini: {detail})"))
    {
        IsDaily = daily;
    }

    public bool IsDaily { get; }
}

// Anahtar yok, geçersiz ya da yetkisiz (HTTP 400/401/403).
public class AiAuthenticationException : AiException
{
    public AiAuthenticationException(string message) : base(message)
    {
    }
}

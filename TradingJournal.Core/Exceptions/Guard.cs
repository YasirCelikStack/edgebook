namespace TradingJournal.Core.Exceptions;

// Tekrar eden doğrulama kontrollerini tek yerde toplar.
// Her property setter'ında aynı if bloğunu yazmak yerine Guard.Positive(...) çağrılır.
public static class Guard
{
    public static decimal Positive(decimal value, string fieldName)
    {
        if (value <= 0)
            throw new DomainException($"{fieldName} 0'dan büyük olmalıdır.");
        return value;
    }

    public static decimal NotNegative(decimal value, string fieldName)
    {
        if (value < 0)
            throw new DomainException($"{fieldName} negatif olamaz.");
        return value;
    }

    public static string NotEmpty(string? value, string fieldName, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{fieldName} boş bırakılamaz.");

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainException($"{fieldName} en fazla {maxLength} karakter olabilir.");
        return trimmed;
    }

    public static int InRange(int value, int min, int max, string fieldName)
    {
        if (value < min || value > max)
            throw new DomainException($"{fieldName} {min} ile {max} arasında olmalıdır.");
        return value;
    }
}

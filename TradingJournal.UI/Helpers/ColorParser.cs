namespace TradingJournal.UI.Helpers;

// "#2E7D32" gibi metinleri Color'a çevirir; geçersiz değerde hata fırlatmaz.
public static class ColorParser
{
    public static bool TryParse(string? hex, out Color color)
    {
        color = Color.Empty;
        if (string.IsNullOrWhiteSpace(hex))
            return false;
        try
        {
            color = ColorTranslator.FromHtml(hex);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}

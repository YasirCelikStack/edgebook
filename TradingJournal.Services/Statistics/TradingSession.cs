namespace TradingJournal.Services.Statistics;

// Piyasa seansları. Birbiriyle çakışmayacak şekilde bölündü; böylece her işlem tek bir seansa düşer
// ve seans istatistikleri toplamı tüm işlemlere eşit olur.
public enum TradingSession
{
    Asia,                   // 19:00-03:00 New York saati
    London,                 // 03:00-08:00
    LondonNewYorkOverlap,   // 08:00-12:00 (iki piyasanın da açık olduğu en hacimli dilim)
    NewYork,                // 12:00-17:00
    OffHours                // 17:00-19:00 (piyasa geçişi, düşük hacim)
}

// ICT killzone'ları: seans içindeki en hareketli zaman dilimleri.
public enum Killzone
{
    None,
    Asia,           // 20:00-00:00 New York saati
    LondonOpen,     // 02:00-05:00
    NewYorkAm,      // 07:00-10:00
    LondonClose,    // 10:00-12:00
    NewYorkPm       // 13:30-16:00
}

// İşlemin açılış zamanından seansı ve killzone'u bulur.
// Forex seans saatleri New York saatine göre tanımlıdır. New York ve Türkiye'nin yaz/kış saati
// farklı günlerde değiştiği için Türkiye saatine sabit bir fark eklemek yerine zaman dilimi
// dönüşümü yapılır (TimeZoneInfo). Hesap dakika hassasiyetindedir: 13:29 NY AM dışı, 13:30 NY PM.
public static class SessionClassifier
{
    private static readonly TimeZoneInfo NewYorkZone = FindNewYorkZone();

    private static TimeZoneInfo FindNewYorkZone()
    {
        foreach (string id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }
        // Bulunamazsa yaklaşık: UTC-5 (yaz saati hesaba katılmaz).
        return TimeZoneInfo.CreateCustomTimeZone("NY-fallback", TimeSpan.FromHours(-5), "New York", "New York");
    }

    // Veritabanındaki zaman bilgisayarın yerel saatidir (Türkiye); New York saatine çevrilir.
    public static DateTime ToNewYorkTime(DateTime time)
    {
        DateTime source = time.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(time, DateTimeKind.Local) : time;
        return TimeZoneInfo.ConvertTime(source, NewYorkZone);
    }

    private static int MinuteOfDay(DateTime time)
    {
        DateTime ny = ToNewYorkTime(time);
        return ny.Hour * 60 + ny.Minute;
    }

    private static bool Between(int minute, int fromHour, int fromMinute, int toHour, int toMinute)
        => minute >= fromHour * 60 + fromMinute && minute < toHour * 60 + toMinute;

    public static TradingSession GetSession(DateTime openTime)
    {
        int m = MinuteOfDay(openTime);
        if (m >= 19 * 60 || m < 3 * 60) return TradingSession.Asia;
        if (m < 8 * 60) return TradingSession.London;
        if (m < 12 * 60) return TradingSession.LondonNewYorkOverlap;
        if (m < 17 * 60) return TradingSession.NewYork;
        return TradingSession.OffHours;
    }

    public static Killzone GetKillzone(DateTime openTime)
    {
        int m = MinuteOfDay(openTime);
        if (Between(m, 20, 0, 24, 0)) return Killzone.Asia;
        if (Between(m, 2, 0, 5, 0)) return Killzone.LondonOpen;
        if (Between(m, 7, 0, 10, 0)) return Killzone.NewYorkAm;
        if (Between(m, 10, 0, 12, 0)) return Killzone.LondonClose;
        if (Between(m, 13, 30, 16, 0)) return Killzone.NewYorkPm;
        return Killzone.None;
    }

    public static string ToTurkish(this TradingSession session) => session switch
    {
        TradingSession.Asia => "Asya",
        TradingSession.London => "Londra",
        TradingSession.LondonNewYorkOverlap => "Londra + NY",
        TradingSession.NewYork => "New York",
        _ => "Seans dışı"
    };

    public static string ToTurkish(this Killzone killzone) => killzone switch
    {
        Killzone.Asia => "Asya KZ",
        Killzone.LondonOpen => "Londra açılış KZ",
        Killzone.NewYorkAm => "NY sabah KZ",
        Killzone.LondonClose => "Londra kapanış KZ",
        Killzone.NewYorkPm => "NY öğleden sonra KZ",
        _ => "Killzone dışı"
    };
}

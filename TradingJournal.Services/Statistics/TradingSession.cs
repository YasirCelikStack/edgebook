namespace TradingJournal.Services.Statistics;

public enum TradingSession
{
    Asia,
    London,
    NewYork,
    OffHours
}

public static class SessionClassifier
{
    // İşlemin açılış saatine göre seansı bulur. Saatler UTC'dir;
    // veritabanındaki zaman bilgisayarın yerel saati kabul edilip UTC'ye çevrilir.
    //   Asya:     00:00-07:00 UTC (TR 03:00-10:00)
    //   Londra:   07:00-12:00 UTC (TR 10:00-15:00)
    //   New York: 12:00-21:00 UTC (TR 15:00-00:00)
    public static TradingSession GetSession(DateTime openTime)
    {
        DateTime utc = openTime.Kind == DateTimeKind.Utc ? openTime : DateTime.SpecifyKind(openTime, DateTimeKind.Local).ToUniversalTime();
        int hour = utc.Hour;

        if (hour < 7) return TradingSession.Asia;
        if (hour < 12) return TradingSession.London;
        if (hour < 21) return TradingSession.NewYork;
        return TradingSession.OffHours;
    }

    public static string ToTurkish(this TradingSession session) => session switch
    {
        TradingSession.Asia => "Asya",
        TradingSession.London => "Londra",
        TradingSession.NewYork => "New York",
        _ => "Seans dışı"
    };
}

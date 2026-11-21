using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TradingJournal.Data;

// Uygulamanın veritabanı bağlantısını tek yerden üretir.
// Veritabanı dosyası repo klasörüne değil, kullanıcının AppData klasörüne yazılır:
// böylece kişisel işlem verileri yanlışlıkla GitHub'a gitmez.
public static class DbContextFactory
{
    public static string DefaultDatabasePath
    {
        get
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EdgeBook");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "edgebook.db");
        }
    }

    public static TradingJournalDbContext Create(string? databasePath = null)
    {
        var options = new DbContextOptionsBuilder<TradingJournalDbContext>()
            .UseSqlite($"Data Source={databasePath ?? DefaultDatabasePath}")
            .Options;

        return new TradingJournalDbContext(options);
    }
}

// "dotnet ef migrations add ..." komutu çalışırken DbContext'i bu sınıf üzerinden oluşturur.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TradingJournalDbContext>
{
    public TradingJournalDbContext CreateDbContext(string[] args) => DbContextFactory.Create();
}

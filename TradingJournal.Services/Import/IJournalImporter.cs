namespace TradingJournal.Services.Import;

// İşlem kaynağı sözleşmesi. İçe aktarma ekranı ve JournalImportService
// verinin nereden geldiğini bilmez; sadece bu arayüzü kullanır.
public interface IJournalImporter
{
    // Kaynağın adı ("Chart Vault"). Not alanına ve ExternalId önekine yazılır.
    string SourceName { get; }

    Task<IReadOnlyList<ImportedTrade>> FetchAsync(CancellationToken cancellationToken = default);
}

// Bağlantı, giriş veya veri hatası: arayüz bu türü yakalayıp anlaşılır mesaj gösterir.
public class ImportException : Exception
{
    public ImportException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

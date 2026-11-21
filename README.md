# EdgeBook

Trader'ların işlemlerini kaydedip performanslarını analiz ettiği, yapay zeka destekli masaüstü trading journal uygulaması.

> Fırat Üniversitesi Nesne Tabanlı Programlama dersi dönem projesi olarak geliştirilmiştir. Ticari bir ürün değildir.

## Teknolojiler

- C# / .NET 10, Windows Forms
- DevExpress WinForms bileşenleri
- SQLite + Entity Framework Core 10
- Yapay zeka API (işlem örüntüsü analizi)

## Proje yapısı

| Katman | Sorumluluk |
|---|---|
| `TradingJournal.Core` | Entity'ler (`Trade`, `ForexTrade`, `CryptoTrade`, `Account`...), arayüzler, doğrulama |
| `TradingJournal.Data` | EF Core `DbContext`, tablo eşlemeleri, generic repository, migration'lar |
| `TradingJournal.Services` | İş mantığı: istatistikler, risk kuralları, yapay zeka istemcisi |
| `TradingJournal.UI` | DevExpress ile Windows Forms arayüzü |

Bağımlılık yönü tek taraflıdır: **UI → Services → Data → Core**.

## Veritabanı

Tablolar: `Accounts`, `AccountRules`, `Instruments`, `Strategies`, `Tags`, `Trades`, `TradeTags` (çoka-çok ara tablo), `TradeScreenshots`.

`ForexTrade` ve `CryptoTrade`, `Trades` tablosunda `TradeType` sütunuyla ayrılır (Table-Per-Hierarchy).

Veritabanı dosyası `%LocalAppData%\EdgeBook\edgebook.db` konumunda oluşturulur, repoya dahil edilmez.

## Kurulum

1. Visual Studio 2026 ve .NET 10 SDK
2. DevExpress WinForms bileşenleri
3. Reponun klonlanması ve derleme:

```powershell
git clone https://github.com/YasirCelikStack/edgebook.git
cd edgebook
dotnet build
```

*(Çalıştırma ve yapay zeka ayarları geliştikçe eklenecek.)*

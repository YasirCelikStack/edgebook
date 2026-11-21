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

Uygulama ilk açılışta veritabanını kendisi oluşturur (`Database.Migrate()`), ayrıca komut çalıştırmaya gerek yoktur.

> `TradingJournal.UI.csproj` içindeki `DevExpressVersion` değeri, bilgisayarında kurulu DevExpress sürümüyle aynı olmalıdır.

## Arayüz

- **Ana pencere:** Ribbon menü + `GridControl` işlem listesi (otomatik filtre satırı, arama, sütuna göre gruplama, PnL/R ara toplamları). Durum çubuğu, o an filtrelenmiş işlemlerin win rate, profit factor, ortalama R ve max drawdown değerlerini gösterir.
- **İşlem formu:** Enstrüman türüne göre `ForexTrade` (lot) veya `CryptoTrade` (miktar + kaldıraç) oluşturur; etiket, strateji, puan ve not girilir.
- **İşlemi kapat:** `Trade.Close()` ile çıkış fiyatı ve zamanı kaydedilir, sonuç (Net PnL, R) gösterilir.
- **Tanımlar:** Hesap, enstrüman, strateji ve etiket ekranları tek bir generic `EntityListForm<T>` sınıfından türer.

*(Yapay zeka ayarları geliştikçe eklenecek.)*

namespace TradingJournal.Core.Enums;

// Bir işlemin, stratejisinin kontrol listesine uyumu.
public enum RuleCompliance
{
    NotTracked,   // Kontrol listesi doldurulmamış (stratejinin kuralı yok ya da eski işlem)
    Followed,     // Tüm kurallara uyuldu
    Broken        // En az bir kural çiğnendi
}

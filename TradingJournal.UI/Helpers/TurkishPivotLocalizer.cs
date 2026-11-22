using DevExpress.XtraPivotGrid.Localization;

namespace TradingJournal.UI.Helpers;

// Pivot tablodaki İngilizce "Grand Total" yazısını Türkçeleştirir.
// DevExpress metinleri Localizer sınıfları üzerinden verir; sadece değiştirmek istediğimiz metni eziyoruz.
public class TurkishPivotLocalizer : PivotGridLocalizer
{
    public override string GetLocalizedString(PivotGridStringId id) => id switch
    {
        PivotGridStringId.GrandTotal => "Genel toplam",
        _ => base.GetLocalizedString(id)
    };
}

using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;

namespace TradingJournal.UI.Helpers;

// Formlarda tekrar tekrar aynı ayarlarla oluşturulan editörleri tek yerde üretir.
public static class EditorFactory
{
    // Fiyat, lot, bakiye gibi ondalıklı sayılar için.
    public static SpinEdit Decimal(int decimals = 5, decimal min = 0, decimal max = 100_000_000m)
    {
        var spin = new SpinEdit();
        spin.Properties.IsFloatValue = true;
        spin.Properties.MinValue = min;
        spin.Properties.MaxValue = max;
        spin.Properties.Increment = 0.1m;
        spin.Properties.DisplayFormat.FormatType = FormatType.Numeric;
        spin.Properties.DisplayFormat.FormatString = "0." + new string('#', decimals);
        spin.Properties.EditFormat.FormatType = FormatType.Numeric;
        spin.Properties.EditFormat.FormatString = "0." + new string('#', decimals);
        spin.Properties.Buttons.Clear(); // Fiyatı oklarla değil klavyeyle gireriz.
        return spin;
    }

    public static SpinEdit Integer(int min, int max)
    {
        var spin = new SpinEdit();
        spin.Properties.IsFloatValue = false;
        spin.Properties.MinValue = min;
        spin.Properties.MaxValue = max;
        return spin;
    }

    // Tarih + saat seçilebilen alan (dd.MM.yyyy HH:mm).
    public static DateEdit DateTime()
    {
        const string format = "dd.MM.yyyy HH:mm";
        var date = new DateEdit();
        date.Properties.CalendarTimeEditing = DefaultBoolean.True;
        date.Properties.DisplayFormat.FormatType = FormatType.DateTime;
        date.Properties.DisplayFormat.FormatString = format;
        date.Properties.EditFormat.FormatType = FormatType.DateTime;
        date.Properties.EditFormat.FormatString = format;
#pragma warning disable CS0618 // Mask eski API olarak işaretli ama tüm sürümlerde çalışıyor.
        date.Properties.Mask.EditMask = format;
        date.Properties.Mask.UseMaskAsDisplayFormat = true;
#pragma warning restore CS0618
        return date;
    }

    // Enum değerlerinden açılır liste (Long/Short, Demo/Real/PropFirm...).
    public static ImageComboBoxEdit Enum<TEnum>() where TEnum : struct, System.Enum
    {
        var combo = new ImageComboBoxEdit();
        foreach (TEnum value in System.Enum.GetValues<TEnum>())
            combo.Properties.Items.Add(new ImageComboBoxItem(value.ToString(), value));
        return combo;
    }

    // Başka tablodan seçim (hesap, enstrüman, strateji).
    public static LookUpEdit Lookup(string displayMember, bool allowNull = false)
    {
        var lookup = new LookUpEdit();
        lookup.Properties.DisplayMember = displayMember;
        lookup.Properties.ValueMember = "Id";
        lookup.Properties.NullText = allowNull ? "(yok)" : "Seçiniz...";
        lookup.Properties.ShowHeader = false;
        lookup.Properties.ShowFooter = false;
        lookup.Properties.Columns.Add(new LookUpColumnInfo(displayMember));
        if (allowNull)
            lookup.Properties.AllowNullInput = DefaultBoolean.True;
        return lookup;
    }
}

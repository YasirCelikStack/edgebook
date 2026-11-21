using DevExpress.XtraEditors;
using TradingJournal.Core.Exceptions;
using TradingJournal.Data;

namespace TradingJournal.UI.Helpers;

// Hata türüne göre farklı mesaj gösterir:
//   DomainException     -> kullanıcı yanlış değer girdi (uyarı)
//   DataAccessException -> veritabanı işlemi başarısız (hata)
//   diğerleri           -> beklenmeyen hata
public static class ErrorHelper
{
    public static void Show(IWin32Window? owner, Exception ex)
    {
        switch (ex)
        {
            case DomainException domain:
                XtraMessageBox.Show(owner, domain.Message, "Geçersiz değer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;

            case DataAccessException data:
                string detail = data.InnerException?.InnerException?.Message
                                ?? data.InnerException?.Message ?? string.Empty;
                XtraMessageBox.Show(owner, $"{data.Message}\n\n{detail}", "Veritabanı hatası",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                break;

            default:
                XtraMessageBox.Show(owner, $"Beklenmeyen bir hata oluştu:\n{ex.Message}", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                break;
        }
    }

    public static bool Confirm(IWin32Window owner, string message)
        => XtraMessageBox.Show(owner, message, "Onay",
               MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
}

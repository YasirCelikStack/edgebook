using DevExpress.LookAndFeel;
using DevExpress.XtraEditors;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Data;
using TradingJournal.UI.Forms;
using TradingJournal.UI.Helpers;

namespace TradingJournal.UI;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        // WXI temasının koyu paleti; kendi çizdiğimiz kartlar Theme sınıfındaki renkleri kullanır.
        UserLookAndFeel.Default.SetSkinStyle("WXI", "Darkness");
        WindowsFormsSettings.DefaultFont = new Font("Segoe UI", 9.75f);

        // Yakalanmayan hatalar uygulamayı çökertmesin, kullanıcıya anlaşılır mesaj gösterilsin.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ErrorHelper.Show(null, e.Exception);

        // Veritabanı yoksa oluşturur, eksik migration varsa uygular.
        try
        {
            using var db = DbContextFactory.Create();
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            XtraMessageBox.Show($"Veritabanı açılamadı:\n{ex.Message}", "EdgeBook",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new MainForm());
    }
}

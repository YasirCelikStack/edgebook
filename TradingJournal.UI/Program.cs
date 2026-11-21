using DevExpress.LookAndFeel;
using DevExpress.XtraEditors;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Data;
using TradingJournal.UI.Forms;
using TradingJournal.UI.Helpers;
using Velopack;

namespace TradingJournal.UI;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Velopack: kurulum, güncelleme ve kaldırma sırasında kendi işlerini yapıp çıkar.
        // Her zaman Main'in ilk satırı olmalı.
        VelopackApp.Build().Run();

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

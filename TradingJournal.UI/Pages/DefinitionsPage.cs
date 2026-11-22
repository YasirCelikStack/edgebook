using System.ComponentModel;
using DevExpress.XtraEditors;
using DevExpress.XtraTab;
using TradingJournal.Services.DataBackup;
using TradingJournal.Services.Demo;
using TradingJournal.UI.Controls;
using TradingJournal.UI.Forms.Lookups;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Pages;

// Hesap, enstrüman, strateji ve etiket listeleri sekmeler halinde.
// Var olan liste formları pencere olarak değil, sekmenin içine gömülü kontrol olarak açılır.
[DesignerCategory("Code")]
public class DefinitionsPage : PageBase
{
    private readonly DemoDataGenerator _demo = new();
    private readonly XtraTabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly FlatButton _demoCreate = new("Örnek veri oluştur", "\uE768");
    private readonly FlatButton _demoDelete = new("Örnek verileri sil", "\uE74D") { Danger = true };
    private readonly BackupService _backup = new();
    private readonly FlatButton _backupCreate = new("Yedek al", "\uE74E");
    private readonly FlatButton _backupRestore = new("Yedekten yükle", "\uE777");

    public DefinitionsPage(AppState state) : base(state)
    {
        var tabs = _tabs;
        AddTab(tabs, "Hesaplar", new AccountListForm());
        AddTab(tabs, "Enstrümanlar", new InstrumentListForm());
        AddTab(tabs, "Stratejiler", new StrategyListForm());
        AddTab(tabs, "Etiketler", new TagListForm());

        _demoCreate.Click += async (_, _) => await CreateDemoAsync();
        _demoDelete.Click += async (_, _) => await DeleteDemoAsync();
        var demoBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.Background, WrapContents = false };
        demoBar.Controls.Add(_demoCreate);
        demoBar.Controls.Add(_demoDelete);
        demoBar.Controls.Add(new Label
        {
            Text = "Deneme ve sunum için ayrı bir hesaba ~180 örnek işlem ekler.",
            AutoSize = true, ForeColor = Theme.TextMuted, Font = Theme.Small, Margin = new Padding(8, 12, 0, 0)
        });

        // Yedekleme düğmeleri sağa yaslı.
        _backupCreate.Click += async (_, _) => await CreateBackupAsync();
        _backupRestore.Click += async (_, _) => await RestoreBackupAsync();
        var backupBar = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, BackColor = Theme.Background, WrapContents = false };
        backupBar.Controls.Add(_backupCreate);
        backupBar.Controls.Add(_backupRestore);

        var topBar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.Background };
        demoBar.Dock = DockStyle.Fill;
        topBar.Controls.Add(demoBar);
        topBar.Controls.Add(backupBar);

        Controls.Add(tabs);
        Controls.Add(topBar);
    }

    public override string Title => "Tanımlar";
    public override string Subtitle => "Hesaplar, risk kuralları, enstrümanlar, stratejiler ve etiketler";

    // Liste formları kendi verilerini açılırken yükler; burada sadece örnek veri düğmeleri güncellenir.
    public override async Task RefreshAsync()
    {
        bool exists = await _demo.ExistsAsync();
        _demoCreate.Enabled = !exists;
        _demoDelete.Enabled = exists;
    }

    private async Task CreateDemoAsync()
    {
        bool ok = ErrorHelper.Confirm(FindForm()!,
            $"\"{DemoDataGenerator.AccountName}\" adında yeni bir hesap oluşturulacak ve içine son 4 aya yayılmış " +
            "yaklaşık 180 örnek işlem eklenecek (eksikse enstrüman, strateji ve etiketler de eklenir).\n\n" +
            "Kendi hesaplarına ve işlemlerine dokunulmaz. \"Örnek verileri sil\" ile hepsi tek seferde kaldırılır.\n\nDevam edilsin mi?");
        if (!ok) return;

        try
        {
            UseWaitCursor = true;
            int count = await _demo.GenerateAsync();
            XtraMessageBox.Show(FindForm(),
                $"{count} örnek işlem eklendi.\n\nÜst çubuktaki hesap seçiciden \"{DemoDataGenerator.AccountName}\" hesabını seçip " +
                "Panel, Analiz ve Yapay zeka koçu sayfalarına bakabilirsin.",
                "Örnek veri", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
        }
        finally
        {
            UseWaitCursor = false;
        }
        await RefreshAsync();
        ReloadTabs();
    }

    private async Task DeleteDemoAsync()
    {
        if (!ErrorHelper.Confirm(FindForm()!,
                $"\"{DemoDataGenerator.AccountName}\" hesabı ve içindeki tüm örnek işlemler silinecek. " +
                "Kendi verilerin etkilenmez. Devam edilsin mi?"))
            return;

        try
        {
            await _demo.DeleteAsync();
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
        }
        await RefreshAsync();
        ReloadTabs();
    }

    // ---------------- Yedekleme ----------------

    private async Task CreateBackupAsync()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "EdgeBook yedeğini kaydet",
            Filter = "EdgeBook yedeği (*.zip)|*.zip",
            FileName = $"EdgeBook-yedek-{DateTime.Now:yyyy-MM-dd}.zip"
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

        _backupCreate.Enabled = false;
        UseWaitCursor = true;
        try
        {
            BackupInfo info = await Task.Run(() => _backup.CreateAsync(dialog.FileName, AppUpdater.CurrentVersion));
            XtraMessageBox.Show(FindForm(),
                $"Yedek kaydedildi: {info.Trades} işlem, {info.Screenshots} ekran görüntüsü.\n\n{dialog.FileName}\n\n" +
                "Bu dosyayı bilgisayar dışında bir yerde (USB, Google Drive...) saklarsan bilgisayar değişse de verin kaybolmaz.",
                "Yedek al", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
        }
        finally
        {
            UseWaitCursor = false;
            _backupCreate.Enabled = true;
        }
    }

    private async Task RestoreBackupAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Geri yüklenecek EdgeBook yedeğini seç",
            Filter = "EdgeBook yedeği (*.zip)|*.zip"
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            BackupInfo info = _backup.ReadInfo(dialog.FileName);
            bool ok = ErrorHelper.Confirm(FindForm()!,
                $"Yedek: {info.CreatedAt:dd.MM.yyyy HH:mm} (EdgeBook v{info.AppVersion})\n" +
                $"İçinde {info.Trades} işlem ve {info.Screenshots} ekran görüntüsü var.\n\n" +
                "Şu anki tüm verilerin (hesaplar, işlemler, görseller) bu yedektekilerle DEĞİŞTİRİLECEK. " +
                "Güvenlik için mevcut verin önce otomatik olarak yedeklenir.\n\n" +
                "İşlem bitince EdgeBook yeniden başlar. Devam edilsin mi?");
            if (!ok) return;

            _backupRestore.Enabled = false;
            UseWaitCursor = true;
            string safetyCopy = await Task.Run(() => _backup.RestoreAsync(dialog.FileName, AppUpdater.CurrentVersion));
            UseWaitCursor = false;

            XtraMessageBox.Show(FindForm(),
                $"Yedek geri yüklendi. EdgeBook şimdi yeniden başlayacak.\n\nÖnceki verinin kopyası:\n{safetyCopy}",
                "Yedekten yükle", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Restart();
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
        }
        finally
        {
            UseWaitCursor = false;
            _backupRestore.Enabled = true;
        }
    }

    // Sekmelerdeki listeler (hesaplar, stratejiler...) yeni verileri göstersin diye yeniden açılır.
    private void ReloadTabs()
    {
        int selected = _tabs.SelectedTabPageIndex;
        foreach (XtraTabPage page in _tabs.TabPages.Cast<XtraTabPage>().ToList())
        {
            _tabs.TabPages.Remove(page);
            page.Dispose();
        }
        AddTab(_tabs, "Hesaplar", new AccountListForm());
        AddTab(_tabs, "Enstrümanlar", new InstrumentListForm());
        AddTab(_tabs, "Stratejiler", new StrategyListForm());
        AddTab(_tabs, "Etiketler", new TagListForm());
        _tabs.SelectedTabPageIndex = Math.Min(selected, _tabs.TabPages.Count - 1);
    }

    private static void AddTab(XtraTabControl tabs, string title, Form form)
    {
        var page = new XtraTabPage { Text = title };
        form.TopLevel = false;
        form.FormBorderStyle = FormBorderStyle.None;
        form.Dock = DockStyle.Fill;
        page.Controls.Add(form);
        tabs.TabPages.Add(page);
        form.Show();
    }
}

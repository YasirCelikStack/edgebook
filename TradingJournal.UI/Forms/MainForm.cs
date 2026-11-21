using System.ComponentModel;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using TradingJournal.Core.Entities;
using TradingJournal.UI.Controls;
using TradingJournal.UI.Forms.Trades;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Pages;
using TradingJournal.UI.Theming;
using Velopack;

namespace TradingJournal.UI.Forms;

// Ana pencere: solda menü, üstte sayfa başlığı + hesap seçici + "Yeni işlem",
// ortada seçili sayfa. Sayfalar bir kez oluşturulur ve tekrar kullanılır.
[DesignerCategory("Code")]
public class MainForm : XtraForm
{
    private readonly AppState _state = new();
    private readonly Sidebar _sidebar = new();
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(16, 0, 24, 24) };
    private readonly Panel _topBar = new() { Dock = DockStyle.Top, Height = 84, BackColor = Theme.Background };
    private readonly Label _title = new() { AutoSize = true, Font = Theme.Title, ForeColor = Theme.Text, Location = new Point(22, 18) };
    private readonly Label _subtitle = new() { AutoSize = true, Font = Theme.Body, ForeColor = Theme.TextMuted, Location = new Point(24, 50) };
    private readonly ImageComboBoxEdit _accountPicker = new() { Width = 230 };
    private readonly FlatButton _newTradeButton = new("Yeni işlem", Theme.Glyph.Add, primary: true);

    // Yeni sürüm çıkınca en üstte görünen şerit.
    private readonly Panel _updateBanner = new() { Dock = DockStyle.Top, Height = 48, BackColor = Theme.Tint(Theme.Accent, 0.35, Theme.Background), Visible = false };
    private readonly Label _updateLabel = new() { AutoSize = true, ForeColor = Theme.Text, Font = Theme.BodyBold, Margin = new Padding(16, 14, 16, 0) };
    private readonly FlatButton _updateButton = new("Şimdi güncelle", "\uE896", primary: true);
    private readonly FlatButton _laterButton = new("Sonra");
    private UpdateInfo? _pendingUpdate;

    private readonly Dictionary<string, PageBase> _pages;
    private PageBase? _currentPage;
    private bool _loadingAccounts;

    public MainForm()
    {
        // Başlıkta derleme saati: hangi sürümün çalıştığı (eski derleme mi, yeni mi) her zaman görülsün.
        DateTime built = File.GetLastWriteTime(Application.ExecutablePath.Replace(".exe", ".dll"));
        Text = $"EdgeBook — Trading Journal   ·   derleme {built:dd.MM HH:mm}";
        ClientSize = new Size(1440, 860);
        MinimumSize = new Size(1150, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        KeyPreview = true;

        var trades = new TradesPage(_state);
        var calendar = new CalendarPage(_state);
        calendar.DaySelected += async (_, day) =>
        {
            _sidebar.Select("trades");
            await Task.Yield();
            trades.FilterByDay(day);
        };

        _pages = new Dictionary<string, PageBase>
        {
            ["dashboard"] = new DashboardPage(_state),
            ["trades"] = trades,
            ["calendar"] = calendar,
            ["analytics"] = new AnalyticsPage(_state),
            ["coach"] = new CoachPage(_state),
            ["definitions"] = new DefinitionsPage(_state),
            ["import"] = new ImportPage(_state)
        };

        _sidebar.AddItem("dashboard", "Panel", Theme.Glyph.Dashboard);
        _sidebar.AddItem("trades", "İşlemler", Theme.Glyph.List);
        _sidebar.AddItem("calendar", "Takvim", Theme.Glyph.Calendar);
        _sidebar.AddItem("analytics", "Analiz", "\uE9D2");
        _sidebar.AddItem("coach", "Yapay zeka koçu", "\uE99A");
        _sidebar.AddItem("definitions", "Tanımlar", Theme.Glyph.Settings);
        _sidebar.AddItem("import", "Chart Vault", "\uE895");
        _sidebar.ItemSelected += async (_, key) => await NavigateAsync(key);

        BuildTopBar();
        BuildUpdateBanner();
        _sidebar.VersionText = $"v{AppUpdater.CurrentVersion}";
        _sidebar.VersionClicked += async (_, _) => await CheckForUpdatesAsync(userRequested: true);

        // Sıra: önce Fill olan içerik, sonra üst çubuk, en son sol menü.
        var right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        right.Controls.Add(_content);
        right.Controls.Add(_topBar);
        right.Controls.Add(_updateBanner);
        Controls.Add(right);
        Controls.Add(_sidebar);

        _state.AccountChanged += async (_, _) =>
        {
            if (_currentPage is not null)
                await SafeRefreshAsync(_currentPage);
        };
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await LoadAccountsAsync();
        _sidebar.Select("dashboard");

        // Açılışta sessizce yeni sürüm var mı bak; varsa üstte şerit çıkar.
        _ = CheckForUpdatesAsync(userRequested: false);
    }

    private void BuildTopBar()
    {
        _accountPicker.Properties.Buttons[0].Kind = ButtonPredefines.Combo;
        _accountPicker.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
        // Seçim hem listeden tıklayınca (SelectedIndexChanged) hem değer değişince (EditValueChanged) okunur;
        // SelectAccount aynı hesap için iki kez çağrılsa da sayfayı bir kez yeniler.
        _accountPicker.SelectedIndexChanged += (_, _) => OnAccountPicked();
        _accountPicker.EditValueChanged += (_, _) => OnAccountPicked();

        _newTradeButton.Click += async (_, _) => await NewTradeAsync();

        _topBar.Controls.AddRange([_title, _subtitle, _accountPicker, _newTradeButton]);
        _topBar.Resize += (_, _) => PositionTopBarControls();
        PositionTopBarControls();
    }

    // ---------------- Güncelleme ----------------

    private void BuildUpdateBanner()
    {
        _updateButton.Click += async (_, _) => await InstallUpdateAsync();
        _laterButton.Click += (_, _) => _updateBanner.Visible = false;

        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(4, 4, 0, 0) };
        row.Controls.Add(_updateLabel);
        row.Controls.Add(_updateButton);
        row.Controls.Add(_laterButton);
        _updateBanner.Controls.Add(row);
    }

    private async Task CheckForUpdatesAsync(bool userRequested)
    {
        if (!AppUpdater.IsInstalled)
        {
            if (userRequested)
                XtraMessageBox.Show(this,
                    $"Bu bir geliştirme sürümü (v{AppUpdater.CurrentVersion}). Güncellemeler sadece kurulum dosyasıyla kurulan sürümde denetlenir.",
                    "Güncellemeler", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        UpdateInfo? update = await AppUpdater.CheckAsync();
        if (update is null)
        {
            if (userRequested)
                XtraMessageBox.Show(this, $"EdgeBook güncel (v{AppUpdater.CurrentVersion}).", "Güncellemeler",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _pendingUpdate = update;
        _updateLabel.Text = $"EdgeBook {update.TargetFullRelease.Version} yayında (sende v{AppUpdater.CurrentVersion}).";
        _updateButton.Enabled = true;
        _updateBanner.Visible = true;
    }

    private async Task InstallUpdateAsync()
    {
        if (_pendingUpdate is null) return;
        _updateButton.Enabled = false;
        _laterButton.Enabled = false;
        try
        {
            await AppUpdater.DownloadAndRestartAsync(_pendingUpdate,
                percent => BeginInvoke(new Action(() => _updateLabel.Text = $"Yeni sürüm indiriliyor... %{percent}")));
            // Buraya gelinmez: uygulama yeni sürümle yeniden başlar.
        }
        catch (Exception ex)
        {
            _updateLabel.Text = "Güncelleme indirilemedi. Daha sonra tekrar dene.";
            _updateButton.Enabled = true;
            _laterButton.Enabled = true;
            ErrorHelper.Show(this, ex);
        }
    }

    private void OnAccountPicked()
    {
        if (_loadingAccounts) return;
        object? value = _accountPicker.EditValue
                        ?? (_accountPicker.SelectedItem as ImageComboBoxItem)?.Value;
        int id = value is int v ? v : -1;
        _state.SelectAccount(id < 0 ? null : id);
        UpdateSubtitle();
    }

    // Alt başlıkta hangi hesabın gösterildiği de yazar; seçimin işe yaradığı ekrandan görülür.
    private void UpdateSubtitle()
    {
        if (_currentPage is null) return;
        string scope = _state.AccountId is null
            ? "Tüm hesaplar"
            : (_accountPicker.SelectedItem as ImageComboBoxItem)?.Description ?? "Seçili hesap";
        _subtitle.Text = _currentPage is DefinitionsPage or ImportPage
            ? _currentPage.Subtitle
            : $"{_currentPage.Subtitle}   ·   {scope}";
    }

    private void PositionTopBarControls()
    {
        _newTradeButton.Location = new Point(_topBar.Width - _newTradeButton.Width - 24, 24);
        _accountPicker.Location = new Point(_newTradeButton.Left - _accountPicker.Width - 12, 30);
    }

    // Hesap listesini yeniler; seçili hesabı korur (Tanımlar'da hesap eklenmiş/silinmiş olabilir).
    private async Task LoadAccountsAsync()
    {
        List<Account> accounts;
        try
        {
            accounts = await _state.LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(this, ex);
            return;
        }

        _loadingAccounts = true;
        _accountPicker.Properties.Items.Clear();
        // DİKKAT: new ImageComboBoxItem("metin", sayı) yazılırsa sayı "değer" değil "ikon numarası" olarak
        // anlaşılıyor (DevExpress'in (string, int imageIndex) kurucusu). Değer boş kalınca seçim hiçbir şeyi
        // değiştirmiyordu. Bu yüzden Description ve Value açıkça atanıyor.
        _accountPicker.Properties.Items.Add(new ImageComboBoxItem { Description = "Tüm hesaplar", Value = -1 });
        foreach (Account account in accounts)
            _accountPicker.Properties.Items.Add(new ImageComboBoxItem { Description = $"{account.Name} · {account.Type}", Value = account.Id });

        bool stillExists = _state.AccountId is int id && accounts.Any(a => a.Id == id);
        _accountPicker.EditValue = stillExists ? _state.AccountId!.Value : -1;
        _loadingAccounts = false;

        if (!stillExists)
            _state.SelectAccount(null);
    }

    private async Task NavigateAsync(string key)
    {
        if (!_pages.TryGetValue(key, out PageBase? page))
            return;

        // Tanımlar sayfasından çıkarken hesaplar değişmiş olabilir.
        if (_currentPage is DefinitionsPage or ImportPage)
            await LoadAccountsAsync();

        if (_currentPage != page)
        {
            _content.SuspendLayout();
            _content.Controls.Clear();
            _content.Controls.Add(page);
            _content.ResumeLayout();
            _currentPage = page;
        }

        _title.Text = page.Title;
        UpdateSubtitle();
        await SafeRefreshAsync(page);
    }

    private async Task SafeRefreshAsync(PageBase page)
    {
        try
        {
            UseWaitCursor = true;
            await page.RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(this, ex);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task NewTradeAsync()
    {
        using var form = new TradeEditForm();
        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        // Yeni işlemi İşlemler sayfasında seçili göster: hemen SS eklenebilsin.
        var trades = (TradesPage)_pages["trades"];
        _sidebar.Select("trades");
        await Task.Yield();
        await trades.RefreshAsync();
        if (form.SavedTradeId is int id)
            trades.SelectTrade(id);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.N))
        {
            _ = NewTradeAsync();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}

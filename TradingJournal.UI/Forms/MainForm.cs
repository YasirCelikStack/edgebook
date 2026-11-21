using System.ComponentModel;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using TradingJournal.Core.Entities;
using TradingJournal.UI.Controls;
using TradingJournal.UI.Forms.Trades;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Pages;
using TradingJournal.UI.Theming;

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

    private readonly Dictionary<string, PageBase> _pages;
    private PageBase? _currentPage;
    private bool _loadingAccounts;

    public MainForm()
    {
        Text = "EdgeBook — Trading Journal";
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
            ["definitions"] = new DefinitionsPage(_state),
            ["import"] = new ImportPage(_state)
        };

        _sidebar.AddItem("dashboard", "Panel", Theme.Glyph.Dashboard);
        _sidebar.AddItem("trades", "İşlemler", Theme.Glyph.List);
        _sidebar.AddItem("calendar", "Takvim", Theme.Glyph.Calendar);
        _sidebar.AddItem("analytics", "Analiz", "\uE9D2");
        _sidebar.AddItem("definitions", "Tanımlar", Theme.Glyph.Settings);
        _sidebar.AddItem("import", "Chart Vault", "\uE895");
        _sidebar.ItemSelected += async (_, key) => await NavigateAsync(key);

        BuildTopBar();

        // Sıra: önce Fill olan içerik, sonra üst çubuk, en son sol menü.
        var right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        right.Controls.Add(_content);
        right.Controls.Add(_topBar);
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
    }

    private void BuildTopBar()
    {
        _accountPicker.Properties.Buttons[0].Kind = ButtonPredefines.Combo;
        _accountPicker.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
        _accountPicker.EditValueChanged += (_, _) =>
        {
            if (_loadingAccounts) return;
            int value = _accountPicker.EditValue is int v ? v : -1;
            _state.SelectAccount(value < 0 ? null : value);
        };

        _newTradeButton.Click += async (_, _) => await NewTradeAsync();

        _topBar.Controls.AddRange([_title, _subtitle, _accountPicker, _newTradeButton]);
        _topBar.Resize += (_, _) => PositionTopBarControls();
        PositionTopBarControls();
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
        _accountPicker.Properties.Items.Add(new ImageComboBoxItem("Tüm hesaplar", -1));
        foreach (Account account in accounts)
            _accountPicker.Properties.Items.Add(new ImageComboBoxItem($"{account.Name} · {account.Type}", account.Id));

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
        _subtitle.Text = page.Subtitle;
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

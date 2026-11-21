using System.ComponentModel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.Services.Import;
using TradingJournal.UI.Controls;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Pages;

// Chart Vault'taki Journal kayıtlarını çekip önizler ve seçilen hesaba aktarır.
[DesignerCategory("Code")]
public class ImportPage : PageBase
{
    private readonly JournalImportService _importService = new();
    private readonly ChartVaultSettings _settings = ChartVaultSettings.Load();

    private readonly TextEdit _email = new();
    private readonly TextEdit _password = new();
    private readonly LookUpEdit _account = EditorFactory.Lookup(nameof(Account.Name));
    private readonly SpinEdit _risk = EditorFactory.Decimal(decimals: 2, max: 100);
    private readonly FlatButton _fetchButton = new("Bağlan ve getir", "\uE895", primary: true);
    private readonly FlatButton _importButton = new("İçe aktar", "\uE896", primary: true) { Enabled = false };
    private readonly FlatButton _shotsButton = new("Görselleri indir", "\uE91B") { Enabled = false };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(330, 0), ForeColor = Theme.TextMuted, Font = Theme.Small, Margin = new Padding(0, 8, 0, 0) };

    private readonly GridControl _grid = new() { Dock = DockStyle.Fill };
    private readonly GridView _view;
    private readonly CardPanel _previewCard = new("Önizleme");

    private IJournalImporter? _source;
    private List<ImportPreviewItem> _preview = new();

    public ImportPage(AppState state) : base(state)
    {
        _password.Properties.UseSystemPasswordChar = true;
        _risk.Value = 1m;
        _email.Text = _settings.Email;

        _fetchButton.Click += async (_, _) => await FetchAsync();
        _importButton.Click += async (_, _) => await ImportAsync();
        _shotsButton.Click += async (_, _) => await DownloadShotsOnlyAsync();

        var connection = new CardPanel("Chart Vault bağlantısı") { Dock = DockStyle.Fill };
        connection.Controls.Add(BuildForm());

        _view = new GridView(_grid);
        _grid.MainView = _view;
        ConfigureGrid();
        _previewCard.Dock = DockStyle.Fill;
        _previewCard.Controls.Add(_grid);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(connection, 0, 0);
        root.Controls.Add(_previewCard, 1, 0);
        Controls.Add(root);
    }

    public override string Title => "Chart Vault'tan aktar";
    public override string Subtitle => "Chart Vault Journal'ındaki işlemleri EdgeBook'a çek (Chart Vault'ta hiçbir şey değişmez)";

    public override async Task RefreshAsync()
    {
        List<Account> accounts = await State.LoadAccountsAsync();
        object? selected = _account.EditValue;
        _account.Properties.DataSource = accounts;
        _account.EditValue = selected is int id && accounts.Any(a => a.Id == id) ? id
            : State.AccountId ?? accounts.FirstOrDefault()?.Id;
        UpdatePreviewDisplay();
    }

    // ---------------- Form ----------------

    private Control BuildForm()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            BackColor = Theme.Card,
            AutoScroll = true
        };

        void Field(string caption, Control editor, string? hint = null)
        {
            table.Controls.Add(new Label { Text = caption, AutoSize = true, ForeColor = Theme.TextMuted, Font = Theme.Small, Margin = new Padding(0, 8, 0, 2) });
            editor.Dock = DockStyle.Top;
            table.Controls.Add(editor);
            if (hint is not null)
                table.Controls.Add(new Label { Text = hint, AutoSize = true, MaximumSize = new Size(330, 0), ForeColor = Theme.TextFaint, Font = Theme.Small, Margin = new Padding(0, 2, 0, 0) });
        }

        table.Controls.Add(new Label
        {
            Text = "Chart Vault hesabınla giriş yap. EdgeBook kayıtlarını sadece okur; Chart Vault'ta hiçbir şey değişmez.",
            AutoSize = true, MaximumSize = new Size(330, 0), ForeColor = Theme.TextMuted, Font = Theme.Body,
            Margin = new Padding(0, 0, 0, 4)
        });

        if (!_settings.IsConfigured)
        {
            // Uygulamayı dağıtan kişinin (geliştiricinin) bir kez yapması gereken ayar.
            _fetchButton.Enabled = false;
            table.Controls.Add(new Label
            {
                Text = "Chart Vault bağlantısı bu kurulumda ayarlanmamış. Geliştirici: exe'nin yanına " +
                       "appsettings.Local.json dosyasını ekleyin (örneği: appsettings.Local.example.json).",
                AutoSize = true, MaximumSize = new Size(330, 0), ForeColor = Theme.Warning, Font = Theme.Small,
                Margin = new Padding(0, 4, 0, 4)
            });
        }

        Field("E-posta", _email);
        Field("Şifre", _password, "Şifre kaydedilmez, sadece giriş için kullanılır.");
        _fetchButton.Margin = new Padding(0, 12, 0, 8);
        table.Controls.Add(_fetchButton);

        table.Controls.Add(new Label { Text = "AKTARIM", AutoSize = true, ForeColor = Theme.TextFaint, Font = Theme.SmallBold, Margin = new Padding(0, 16, 0, 0) });
        Field("Hangi hesaba?", _account);
        Field("İşlem başı risk (%)", _risk, "Chart Vault işlemleri R cinsindendir. Lot, bu riske ve stop mesafesine göre hesaplanır: %1 risk ile -1R ≈ bakiyenin %1'i zarar.");
        _importButton.Margin = new Padding(0, 12, 0, 8);
        table.Controls.Add(_importButton);

        table.Controls.Add(new Label { Text = "GÖRSELLER", AutoSize = true, ForeColor = Theme.TextFaint, Font = Theme.Small, Margin = new Padding(0, 12, 0, 0) });
        table.Controls.Add(new Label
        {
            Text = _settings.IsSiteConfigured
                ? "Aktarılan işlemlerin Chart Vault'taki grafik görselleri aktarımla birlikte indirilir. Daha önce aktardığın işlemler için bu düğmeyi kullan."
                : "Görselleri indirmek için Chart Vault site adresi gerekli (appsettings.Local.json → ChartVault:SiteUrl).",
            AutoSize = true, MaximumSize = new Size(330, 0), ForeColor = _settings.IsSiteConfigured ? Theme.TextMuted : Theme.Warning,
            Font = Theme.Small, Margin = new Padding(0, 2, 0, 4)
        });
        _shotsButton.Margin = new Padding(0, 4, 0, 8);
        table.Controls.Add(_shotsButton);

        table.Controls.Add(_status);
        return table;
    }

    // ---------------- Önizleme ----------------

    public sealed record PreviewRow(string State, DateTime OpenTime, string Symbol, string Direction, decimal Entry,
        decimal StopLoss, decimal? TakeProfit, string Result, decimal? R, string? Setup, bool IsNew);

    private void ConfigureGrid()
    {
        _view.OptionsBehavior.Editable = false;
        _view.OptionsBehavior.AutoPopulateColumns = false;
        _view.OptionsView.ShowGroupPanel = false;
        _view.OptionsView.ShowIndicator = false;
        _view.OptionsView.ShowVerticalLines = DefaultBoolean.False;
        _view.RowHeight = 34;
        _view.Appearance.Row.BackColor = Theme.Card;
        _view.Appearance.Empty.BackColor = Theme.Card;

        AddColumn(nameof(PreviewRow.State), "Durum", 80);
        GridColumn date = AddColumn(nameof(PreviewRow.OpenTime), "Tarih", 110);
        date.DisplayFormat.FormatType = FormatType.DateTime;
        date.DisplayFormat.FormatString = "dd.MM.yy HH:mm";
        AddColumn(nameof(PreviewRow.Symbol), "Sembol", 80);
        AddColumn(nameof(PreviewRow.Direction), "Yön", 60);
        AddColumn(nameof(PreviewRow.Entry), "Giriş", 80);
        AddColumn(nameof(PreviewRow.StopLoss), "Stop", 80);
        AddColumn(nameof(PreviewRow.TakeProfit), "Hedef", 80);
        AddColumn(nameof(PreviewRow.Result), "Sonuç", 90);
        GridColumn r = AddColumn(nameof(PreviewRow.R), "R", 60);
        r.DisplayFormat.FormatType = FormatType.Numeric;
        r.DisplayFormat.FormatString = "+0.00;-0.00;0.00";
        AddColumn(nameof(PreviewRow.Setup), "Setup", 140);

        _view.RowCellStyle += (_, e) =>
        {
            if (_view.GetRow(e.RowHandle) is not PreviewRow row) return;
            if (!row.IsNew)
                e.Appearance.ForeColor = Theme.TextFaint;
            else if (e.Column.FieldName == nameof(PreviewRow.R))
                e.Appearance.ForeColor = Theme.ForValue(row.R);
            else if (e.Column.FieldName == nameof(PreviewRow.State))
                e.Appearance.ForeColor = Theme.Accent;
        };

        _view.CustomDrawEmptyForeground += (_, e) =>
        {
            e.Appearance.ForeColor = Theme.TextMuted;
            e.Appearance.TextOptions.HAlignment = HorzAlignment.Center;
            e.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            e.Appearance.DrawString(e.Cache, "Bağlanınca Chart Vault işlemlerin burada listelenecek.\nBekleyen ve gerçekleşmeyen planlar aktarılmaz.", e.Bounds);
            e.Handled = true;
        };
    }

    private GridColumn AddColumn(string field, string caption, int width)
    {
        GridColumn column = _view.Columns.AddVisible(field, caption);
        column.Width = width;
        return column;
    }

    private void UpdatePreviewDisplay()
    {
        _grid.DataSource = _preview.Select(p => new PreviewRow(
            p.AlreadyImported ? "Zaten var" : "Yeni",
            p.Trade.OpenTime,
            p.Trade.Symbol,
            p.Trade.Direction == TradeDirection.Long ? "Long" : "Short",
            p.Trade.EntryPrice,
            p.Trade.StopLoss,
            p.Trade.TakeProfit,
            p.Trade.SourceStatus,
            p.Trade.ResultR,
            p.Trade.Setup,
            !p.AlreadyImported)).ToList();

        int newCount = _preview.Count(p => !p.AlreadyImported);
        _previewCard.Hint = _preview.Count == 0 ? null : $"{_preview.Count} işlem · {newCount} yeni";
        _previewCard.Invalidate();
        _importButton.Text = newCount == 0 ? "İçe aktar" : $"{newCount} işlemi içe aktar";
        _importButton.Enabled = newCount > 0;
    }

    // ---------------- Komutlar ----------------

    private async Task FetchAsync()
    {
        _settings.Email = _email.Text.Trim();

        _fetchButton.Enabled = false;
        SetStatus("Chart Vault'a bağlanılıyor...", Theme.TextMuted);
        try
        {
            _source = new ChartVaultApiImporter(_settings, _email.Text, _password.Text);
            IReadOnlyList<ImportedTrade> trades = await _source.FetchAsync();
            _settings.Save(); // Giriş başarılıysa e-postayı hatırla (şifre kaydedilmez).
            _shotsButton.Enabled = _settings.IsSiteConfigured;
            _preview = await _importService.PreviewAsync(trades);
            UpdatePreviewDisplay();
            SetStatus(trades.Count == 0
                ? "Bağlantı başarılı ama aktarılacak işlem yok (sadece girilmiş işlemler aktarılır)."
                : $"Bağlantı başarılı: {trades.Count} işlem bulundu.", Theme.Profit);
        }
        catch (ImportException ex)
        {
            SetStatus(ex.Message, Theme.Loss);
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
            SetStatus("Bağlantı başarısız.", Theme.Loss);
        }
        finally
        {
            _fetchButton.Enabled = true;
        }
    }

    private async Task ImportAsync()
    {
        if (_source is null) return;
        if (_account.EditValue is not int accountId)
        {
            SetStatus("Önce bir hesap seç.", Theme.Warning);
            return;
        }

        _importButton.Enabled = false;
        try
        {
            ImportResult result = await _importService.ImportAsync(_source, _preview.Select(p => p.Trade), accountId, _risk.Value);
            string message = $"{result.Added} işlem eklendi, {result.Skipped} tanesi zaten vardı.";
            if (result.Failed > 0)
                message += $" {result.Failed} işlem aktarılamadı:\n" + string.Join("\n", result.Errors.Take(5));
            SetStatus(message, result.Failed > 0 ? Theme.Warning : Theme.Profit);

            _preview = await _importService.PreviewAsync(_preview.Select(p => p.Trade));
            UpdatePreviewDisplay();

            // Aktarımın hemen ardından görseller de indirilir.
            if (_source is ChartVaultApiImporter chartVault && _settings.IsSiteConfigured)
            {
                (string shotMessage, Color shotColor) = await DownloadShotsAsync(chartVault);
                SetStatus(message + "\n" + shotMessage, result.Failed > 0 ? Theme.Warning : shotColor);
            }
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
            UpdatePreviewDisplay();
        }
    }

    private async Task DownloadShotsOnlyAsync()
    {
        if (_source is not ChartVaultApiImporter chartVault)
        {
            SetStatus("Önce \"Bağlan ve getir\" ile Chart Vault'a bağlan.", Theme.Warning);
            return;
        }
        (string message, Color color) = await DownloadShotsAsync(chartVault);
        SetStatus(message, color);
    }

    private async Task<(string Message, Color Color)> DownloadShotsAsync(ChartVaultApiImporter chartVault)
    {
        _shotsButton.Enabled = false;
        var progress = new Progress<string>(text => SetStatus(text, Theme.TextMuted));
        try
        {
            ShotDownloadResult r = await _importService.DownloadChartVaultShotsAsync(chartVault, progress);
            if (r.Downloaded == 0 && r.Failed == 0)
                return (r.AlreadyHad > 0 ? "Görseller zaten indirilmiş." : "Chart Vault'ta bu işlemlere bağlı görsel yok.", Theme.TextMuted);

            string text = $"{r.Downloaded} görsel indirildi." +
                          (r.Failed > 0 ? $" {r.Failed} görsel indirilemedi: {r.Errors.FirstOrDefault()}" : "");
            return (text, r.Failed > 0 ? Theme.Warning : Theme.Profit);
        }
        catch (ImportException ex)
        {
            return (ex.Message, Theme.Warning);
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
            return ("Görseller indirilemedi.", Theme.Loss);
        }
        finally
        {
            _shotsButton.Enabled = _settings.IsSiteConfigured;
        }
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.ForeColor = color;
    }
}

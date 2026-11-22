using System.ComponentModel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Views.Grid.ViewInfo;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.Services.Import;
using TradingJournal.UI.Controls;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Pages;

// Chart Vault'taki Journal kayıtlarını çekip önizler; kullanıcının seçtiği işlemleri (görselleriyle) seçilen hesaba aktarır.
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
    private readonly FlatButton _selectAllButton = new("Tümü") { Enabled = false };
    private readonly FlatButton _selectNoneButton = new("Hiçbiri") { Enabled = false };
    private readonly FlatButton _selectWithShotsButton = new("Görselliler", "\uE91B") { Enabled = false };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(330, 0), ForeColor = Theme.TextMuted, Font = Theme.Small, Margin = new Padding(0, 8, 0, 0) };

    private readonly GridControl _grid = new() { Dock = DockStyle.Fill };
    private readonly GridView _view;
    private readonly CardPanel _previewCard = new("Önizleme");

    private IJournalImporter? _source;
    private List<ImportPreviewItem> _preview = new();
    private List<PreviewRow> _rows = new();
    private HashSet<string>? _withShots; // Görseli olan kayıtlar (ExternalId); bilinmiyorsa null.

    private const string SelectField = nameof(PreviewRow.Selected);
    private const string ShotField = nameof(PreviewRow.HasShot);
    private const string CheckedGlyph = "\uE73A";   // Segoe MDL2: işaretli kutu
    private const string UncheckedGlyph = "\uE739"; // Segoe MDL2: boş kutu

    public ImportPage(AppState state) : base(state)
    {
        _password.Properties.UseSystemPasswordChar = true;
        _risk.Value = 1m;
        _email.Text = _settings.Email;

        _fetchButton.Click += async (_, _) => await FetchAsync();
        _importButton.Click += async (_, _) => await ImportAsync();
        _shotsButton.Click += async (_, _) => await DownloadShotsOnlyAsync();
        _selectAllButton.Click += (_, _) => SetSelection(_ => true);
        _selectNoneButton.Click += (_, _) => SetSelection(_ => false);
        _selectWithShotsButton.Click += (_, _) => SetSelection(r => r.HasShot == true);

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

        table.Controls.Add(new Label { Text = "Seç", AutoSize = true, ForeColor = Theme.TextMuted, Font = Theme.Small, Margin = new Padding(0, 8, 0, 2) });
        var selectionButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0), BackColor = Theme.Card };
        foreach (FlatButton b in new[] { _selectAllButton, _selectNoneButton, _selectWithShotsButton })
        {
            b.Height = 30;
            b.Font = Theme.Small;
            b.Margin = new Padding(0, 0, 6, 0);
            selectionButtons.Controls.Add(b);
        }
        table.Controls.Add(selectionButtons);
        table.Controls.Add(new Label
        {
            Text = "Önizlemede bir satıra tıklayarak işlemi seçebilir ya da seçimi kaldırabilirsin. Sadece seçili işlemler ve onların görselleri aktarılır.",
            AutoSize = true, MaximumSize = new Size(330, 0), ForeColor = Theme.TextFaint, Font = Theme.Small, Margin = new Padding(0, 4, 0, 0)
        });
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

    // Önizleme satırı. Selected değiştirilebilir (kullanıcının seçimi); diğerleri sadece gösterim içindir.
    public sealed class PreviewRow
    {
        public required string ExternalId { get; init; }
        public bool Selected { get; set; }
        public required string State { get; init; }
        public DateTime OpenTime { get; init; }
        public required string Symbol { get; init; }
        public required string Direction { get; init; }
        public decimal Entry { get; init; }
        public decimal StopLoss { get; init; }
        public decimal? TakeProfit { get; init; }
        public required string Result { get; init; }
        public decimal? R { get; init; }
        public string? Setup { get; init; }
        public bool? HasShot { get; init; } // null = bilinmiyor
        public bool IsNew { get; init; }
    }

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

        GridColumn select = AddColumn(SelectField, "", 36);
        select.OptionsColumn.AllowSort = DefaultBoolean.False;
        select.OptionsColumn.AllowMove = false;
        select.OptionsColumn.AllowSize = false;
        select.OptionsColumn.FixedWidth = true;
        select.OptionsColumn.ShowCaption = false;
        select.ToolTip = "Tıkla: tümünü seç / hiçbirini seçme";
        // bool sütunlar varsayılan olarak onay kutusu düzenleyicisiyle çizilir; biz ikonu kendimiz yazıyoruz.
        var iconText = new RepositoryItemTextEdit();
        _grid.RepositoryItems.Add(iconText);
        select.ColumnEdit = iconText;

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
        GridColumn shot = AddColumn(ShotField, "Görsel", 60);
        shot.OptionsColumn.FixedWidth = true;
        shot.ColumnEdit = iconText;
        shot.AppearanceCell.TextOptions.HAlignment = HorzAlignment.Center;

        // Seçim kutusu ve görsel sütunu metin/ikon olarak çizilir (düzenleyici açmaya gerek kalmaz).
        _view.CustomColumnDisplayText += (_, e) =>
        {
            if (e.Column.FieldName == ShotField)
                e.DisplayText = e.Value is bool has ? (has ? "\uE91B" : "—") : "";
            else if (e.Column.FieldName == SelectField)
                e.DisplayText = e.Value is true ? CheckedGlyph : UncheckedGlyph;
        };
        _view.RowCellStyle += (_, e) =>
        {
            if (_view.GetRow(e.RowHandle) is not PreviewRow row) return;
            if (e.Column.FieldName == SelectField || (e.Column.FieldName == ShotField && row.HasShot == true))
            {
                e.Appearance.Font = Theme.Icons;
                e.Appearance.TextOptions.HAlignment = HorzAlignment.Center;
            }

            if (e.Column.FieldName == SelectField)
                e.Appearance.ForeColor = !row.IsNew ? Theme.Border : row.Selected ? Theme.Accent : Theme.TextMuted;
            else if (e.Column.FieldName == ShotField)
                e.Appearance.ForeColor = row.HasShot == true ? (row.IsNew && !row.Selected ? Theme.TextMuted : Theme.Accent) : Theme.TextFaint;
            else if (!row.IsNew || !row.Selected)
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

        // Satıra tıklamak seçimi değiştirir; "Zaten var" satırları seçilemez.
        _view.RowCellClick += (_, e) =>
        {
            if (_view.GetRow(e.RowHandle) is not PreviewRow { IsNew: true } row) return;
            row.Selected = !row.Selected;
            _view.RefreshRow(e.RowHandle);
            UpdateSelectionInfo();
        };

        // Seçim sütununun başlığına tıklamak: hepsi seçiliyse hiçbirini, değilse tümünü seçer.
        _view.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            GridHitInfo hit = _view.CalcHitInfo(e.Location);
            if (!hit.InColumnPanel || hit.Column?.FieldName != SelectField) return;
            bool allSelected = _rows.Where(r => r.IsNew).All(r => r.Selected);
            SetSelection(_ => !allSelected);
            DXMouseEventArgs.GetMouseArgs(e).Handled = true;
        };
    }

    private GridColumn AddColumn(string field, string caption, int width)
    {
        GridColumn column = _view.Columns.AddVisible(field, caption);
        column.Width = width;
        return column;
    }

    // Önizlemeyi yeniden kurar. Kullanıcının önceki seçimleri korunur; ilk kez görülen yeni işlemler seçili gelir.
    private void UpdatePreviewDisplay()
    {
        Dictionary<string, bool> previous = _rows.ToDictionary(r => r.ExternalId, r => r.Selected);

        _rows = _preview.Select(p => new PreviewRow
        {
            ExternalId = p.Trade.ExternalId,
            Selected = !p.AlreadyImported && previous.GetValueOrDefault(p.Trade.ExternalId, true),
            State = p.AlreadyImported ? "Zaten var" : "Yeni",
            OpenTime = p.Trade.OpenTime,
            Symbol = p.Trade.Symbol,
            Direction = p.Trade.Direction == TradeDirection.Long ? "Long" : "Short",
            Entry = p.Trade.EntryPrice,
            StopLoss = p.Trade.StopLoss,
            TakeProfit = p.Trade.TakeProfit,
            Result = p.Trade.SourceStatus,
            R = p.Trade.ResultR,
            Setup = p.Trade.Setup,
            HasShot = _withShots?.Contains(p.Trade.ExternalId),
            IsNew = !p.AlreadyImported
        }).ToList();

        _grid.DataSource = _rows;
        UpdateSelectionInfo();
    }

    private void SetSelection(Func<PreviewRow, bool> rule)
    {
        foreach (PreviewRow row in _rows.Where(r => r.IsNew))
            row.Selected = rule(row);
        _view.RefreshData();
        UpdateSelectionInfo();
    }

    private void UpdateSelectionInfo()
    {
        int newCount = _rows.Count(r => r.IsNew);
        int selected = _rows.Count(r => r.IsNew && r.Selected);
        int selectedWithShot = _rows.Count(r => r.IsNew && r.Selected && r.HasShot == true);

        string hint = _rows.Count == 0 ? "" : $"{_rows.Count} işlem · {newCount} yeni · {selected} seçili";
        if (_withShots is not null && selected > 0)
            hint += $" ({selectedWithShot} görselli)";
        _previewCard.Hint = hint.Length == 0 ? null : hint;
        _previewCard.Invalidate();

        _importButton.Text = selected == 0 ? "İçe aktar" : $"{selected} işlemi içe aktar";
        _importButton.Enabled = selected > 0;
        _selectAllButton.Enabled = _selectNoneButton.Enabled = newCount > 0;
        _selectWithShotsButton.Enabled = newCount > 0 && _withShots is not null;
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
            _rows = new List<PreviewRow>(); // Yeni bağlantı: seçimler sıfırdan başlar.

            // Hangi işlemlerin grafik görseli var? (Önizlemedeki "Görsel" sütunu ve "Görselliler" düğmesi için.)
            _withShots = null;
            if (_source is ChartVaultApiImporter chartVault && _settings.IsSiteConfigured && trades.Count > 0)
            {
                SetStatus("Görsel bilgisi alınıyor...", Theme.TextMuted);
                _withShots = await _importService.GetTradesWithShotsAsync(chartVault, trades);
            }
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
            HashSet<string> selectedIds = _rows.Where(r => r.IsNew && r.Selected).Select(r => r.ExternalId).ToHashSet();
            List<ImportedTrade> selectedTrades = _preview.Where(p => selectedIds.Contains(p.Trade.ExternalId)).Select(p => p.Trade).ToList();
            if (selectedTrades.Count == 0)
            {
                SetStatus("Aktarmak için en az bir işlem seç.", Theme.Warning);
                UpdateSelectionInfo();
                return;
            }

            ImportResult result = await _importService.ImportAsync(_source, selectedTrades, accountId, _risk.Value);
            string message = $"{result.Added} işlem eklendi, {result.Skipped} tanesi zaten vardı.";
            if (result.Failed > 0)
                message += $" {result.Failed} işlem aktarılamadı:\n" + string.Join("\n", result.Errors.Take(5));
            SetStatus(message, result.Failed > 0 ? Theme.Warning : Theme.Profit);

            _preview = await _importService.PreviewAsync(_preview.Select(p => p.Trade));
            UpdatePreviewDisplay();

            // Aktarımın hemen ardından sadece seçilen işlemlerin görselleri indirilir.
            if (_source is ChartVaultApiImporter chartVault && _settings.IsSiteConfigured)
            {
                (string shotMessage, Color shotColor) = await DownloadShotsAsync(chartVault, selectedIds);
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

    // onlyExternalIds null ise EdgeBook'taki tüm Chart Vault işlemlerinin eksik görselleri indirilir.
    private async Task<(string Message, Color Color)> DownloadShotsAsync(ChartVaultApiImporter chartVault,
        IReadOnlyCollection<string>? onlyExternalIds = null)
    {
        _shotsButton.Enabled = false;
        var progress = new Progress<string>(text => SetStatus(text, Theme.TextMuted));
        try
        {
            ShotDownloadResult r = await _importService.DownloadChartVaultShotsAsync(chartVault, progress, onlyExternalIds);
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

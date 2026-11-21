using System.ComponentModel;
using DevExpress.Data;
using DevExpress.Utils;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using TradingJournal.Core.Entities;
using TradingJournal.Data;
using TradingJournal.Data.Repositories;
using TradingJournal.Services.Statistics;
using TradingJournal.UI.Forms.Lookups;
using TradingJournal.UI.Forms.Trades;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Models;

namespace TradingJournal.UI.Forms;

// Uygulamanın ana penceresi: üstte Ribbon menü, ortada filtrelenebilir işlem listesi,
// altta seçili/filtrelenmiş işlemlerin özet istatistikleri.
[DesignerCategory("Code")]
public class MainForm : RibbonForm
{
    private static readonly Color ProfitColor = Color.FromArgb(46, 125, 50);
    private static readonly Color LossColor = Color.FromArgb(198, 40, 40);

    // Arayüz somut sınıfa değil arayüze bağlı: ileride başka bir hesaplayıcı takılabilir.
    private readonly IStatisticsService _statistics = new StatisticsService();

    private readonly RibbonControl _ribbon = new();
    private readonly GridControl _grid = new() { Dock = DockStyle.Fill };
    private readonly GridView _view;

    private BarButtonItem _editButton = null!;
    private BarButtonItem _closeButton = null!;
    private BarButtonItem _deleteButton = null!;
    private readonly BarStaticItem _countLabel = new();
    private readonly BarStaticItem _statsLabel = new();

    // Grid'de gösterilen satırların arkasındaki gerçek Trade nesneleri (istatistik için).
    private Dictionary<int, Trade> _tradesById = new();

    public MainForm()
    {
        Text = "EdgeBook — Trading Journal";
        ClientSize = new Size(1280, 760);
        StartPosition = FormStartPosition.CenterScreen;

        _view = new GridView(_grid);
        _grid.MainView = _view;

        BuildRibbon();
        RibbonStatusBar statusBar = BuildStatusBar();
        ConfigureGrid();

        // Sıra önemli: önce Fill olan grid, sonra kenara yapışanlar eklenir.
        Controls.Add(_grid);
        Controls.Add(statusBar);
        Controls.Add(_ribbon);
        Ribbon = _ribbon;
        StatusBar = statusBar;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await ReloadAsync();
    }

    // ---------------- Ribbon ----------------

    private void BuildRibbon()
    {
        _ribbon.ShowApplicationButton = DefaultBoolean.False;
        _ribbon.ToolbarLocation = RibbonQuickAccessToolbarLocation.Hidden;
        _ribbon.ShowExpandCollapseButton = DefaultBoolean.False;

        var journalPage = new RibbonPage("Günlük");

        var tradeGroup = new RibbonPageGroup("İşlem") { AllowTextClipping = false };
        tradeGroup.ItemLinks.Add(CreateButton("Yeni işlem", "Add", Keys.Control | Keys.N, async () => await NewTradeAsync()));
        _editButton = CreateButton("Düzenle", "Edit", Keys.F2, async () => await EditTradeAsync());
        _closeButton = CreateButton("İşlemi kapat", "Apply", Keys.Control | Keys.K, async () => await CloseTradeAsync());
        _deleteButton = CreateButton("Sil", "Delete", Keys.None, async () => await DeleteTradeAsync());
        tradeGroup.ItemLinks.Add(_editButton);
        tradeGroup.ItemLinks.Add(_closeButton);
        tradeGroup.ItemLinks.Add(_deleteButton);

        var viewGroup = new RibbonPageGroup("Liste") { AllowTextClipping = false };
        viewGroup.ItemLinks.Add(CreateButton("Yenile", "Refresh", Keys.F5, async () => await ReloadAsync()));
        viewGroup.ItemLinks.Add(CreateButton("Filtreleri temizle", "ClearFilter", Keys.None, () =>
        {
            _view.ActiveFilter.Clear();
            _view.ApplyFindFilter(string.Empty);
            return Task.CompletedTask;
        }));

        var lookupGroup = new RibbonPageGroup("Tanımlar") { AllowTextClipping = false };
        lookupGroup.ItemLinks.Add(CreateButton("Hesaplar", "BOContact", Keys.None, () => OpenLookupAsync(new AccountListForm())));
        lookupGroup.ItemLinks.Add(CreateButton("Enstrümanlar", "BOProduct", Keys.None, () => OpenLookupAsync(new InstrumentListForm())));
        lookupGroup.ItemLinks.Add(CreateButton("Stratejiler", "BOTask", Keys.None, () => OpenLookupAsync(new StrategyListForm())));
        lookupGroup.ItemLinks.Add(CreateButton("Etiketler", "Tag", Keys.None, () => OpenLookupAsync(new TagListForm())));

        journalPage.Groups.Add(tradeGroup);
        journalPage.Groups.Add(viewGroup);
        journalPage.Groups.Add(lookupGroup);

        var appearancePage = new RibbonPage("Görünüm");
        var skinGroup = new RibbonPageGroup("Tema");
        var skinGallery = new SkinRibbonGalleryBarItem();
        _ribbon.Items.Add(skinGallery);
        skinGroup.ItemLinks.Add(skinGallery);
        appearancePage.Groups.Add(skinGroup);

        _ribbon.Pages.Add(journalPage);
        _ribbon.Pages.Add(appearancePage);
    }

    private BarButtonItem CreateButton(string caption, string imageName, Keys shortcut, Func<Task> onClick)
    {
        var button = new BarButtonItem
        {
            Caption = caption,
            RibbonStyle = RibbonItemStyles.Large
        };
        // DevExpress'in hazır ikon kütüphanesinden (DX Image Gallery) isimle ikon alır.
        button.ImageOptions.ImageUri.Uri = imageName;
        if (shortcut != Keys.None)
            button.ItemShortcut = new BarShortcut(shortcut);

        button.ItemClick += async (_, _) =>
        {
            try
            {
                await onClick();
            }
            catch (Exception ex)
            {
                ErrorHelper.Show(this, ex);
            }
        };

        _ribbon.Items.Add(button);
        return button;
    }

    private RibbonStatusBar BuildStatusBar()
    {
        var statusBar = new RibbonStatusBar { Ribbon = _ribbon };
        _ribbon.Items.Add(_countLabel);
        _ribbon.Items.Add(_statsLabel);
        statusBar.ItemLinks.Add(_countLabel);
        statusBar.ItemLinks.Add(_statsLabel, true);
        return statusBar;
    }

    // ---------------- Grid ----------------

    private void ConfigureGrid()
    {
        _view.OptionsBehavior.Editable = false;
        _view.OptionsBehavior.AutoPopulateColumns = false;
        _view.OptionsView.ShowAutoFilterRow = true;
        _view.OptionsView.ShowFooter = true;
        _view.OptionsView.ShowGroupPanel = true;
        _view.OptionsView.ColumnAutoWidth = false;
        _view.OptionsFind.AlwaysVisible = true;
        _view.OptionsSelection.EnableAppearanceFocusedCell = false;
        _view.GroupPanelText = "Gruplamak için bir sütun başlığını buraya sürükleyin (örn. Seans, Strateji)";

        AddDateColumn(nameof(TradeRow.OpenTime), "Açılış");
        AddColumn(nameof(TradeRow.Account), "Hesap", width: 130);
        GridColumn symbol = AddColumn(nameof(TradeRow.Symbol), "Sembol", width: 85);
        AddColumn(nameof(TradeRow.Market), "Piyasa", width: 65);
        AddColumn(nameof(TradeRow.Direction), "Yön", width: 60);
        AddColumn(nameof(TradeRow.Session), "Seans", width: 80);
        AddColumn(nameof(TradeRow.Strategy), "Strateji", width: 140);
        AddColumn(nameof(TradeRow.EntryPrice), "Giriş", "0.#####");
        AddColumn(nameof(TradeRow.StopLoss), "Stop", "0.#####");
        AddColumn(nameof(TradeRow.TakeProfit), "Hedef", "0.#####");
        AddColumn(nameof(TradeRow.ExitPrice), "Çıkış", "0.#####");
        AddColumn(nameof(TradeRow.Size), "Boyut", width: 100);
        AddColumn(nameof(TradeRow.Risk), "Risk", "n2");
        GridColumn pnl = AddColumn(nameof(TradeRow.NetPnL), "Net PnL", "n2", width: 90);
        GridColumn r = AddColumn(nameof(TradeRow.RMultiple), "R", "0.00", width: 60);
        AddColumn(nameof(TradeRow.Status), "Durum", width: 65);
        AddColumn(nameof(TradeRow.Tags), "Etiketler", width: 160);
        AddColumn(nameof(TradeRow.Stars), "Puan", width: 70);
        AddDateColumn(nameof(TradeRow.CloseTime), "Kapanış");

        // Alt satırdaki toplamlar ve grup başlıklarındaki ara toplamlar.
        symbol.Summary.Add(SummaryItemType.Count, symbol.FieldName, "{0} işlem");
        pnl.Summary.Add(SummaryItemType.Sum, pnl.FieldName, "{0:n2}");
        r.Summary.Add(SummaryItemType.Sum, r.FieldName, "{0:0.00}R");
        _view.GroupSummary.Add(SummaryItemType.Count, symbol.FieldName, null, "({0} işlem)");
        _view.GroupSummary.Add(SummaryItemType.Sum, pnl.FieldName, pnl, "{0:n2}");
        _view.GroupSummary.Add(SummaryItemType.Sum, r.FieldName, r, "{0:0.00}R");

        _view.RowCellStyle += (_, e) =>
        {
            if (e.Column.FieldName is nameof(TradeRow.NetPnL) or nameof(TradeRow.RMultiple) && e.CellValue is decimal value)
            {
                if (value > 0) e.Appearance.ForeColor = ProfitColor;
                else if (value < 0) e.Appearance.ForeColor = LossColor;
                e.Appearance.FontStyleDelta = FontStyle.Bold;
            }
        };

        _view.CustomDrawEmptyForeground += (_, e) =>
        {
            string text = _tradesById.Count == 0
                ? "Henüz işlem yok. \"Yeni işlem\" ile ilk işlemini ekle (Ctrl+N)."
                : "Filtreye uyan işlem yok.";
            e.Appearance.ForeColor = Color.Gray;
            e.Appearance.TextOptions.HAlignment = HorzAlignment.Center;
            e.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            e.Appearance.DrawString(e.Cache, text, e.Bounds);
            e.Handled = true;
        };

        _view.DoubleClick += async (_, _) => await EditTradeAsync();
        _view.FocusedRowChanged += (_, _) => UpdateButtonStates();
        _view.ColumnFilterChanged += (_, _) => UpdateStatistics();
    }

    private GridColumn AddColumn(string fieldName, string caption, string? numberFormat = null, int width = 75)
    {
        GridColumn column = _view.Columns.AddVisible(fieldName, caption);
        column.Width = width;
        if (numberFormat is not null)
        {
            column.DisplayFormat.FormatType = FormatType.Numeric;
            column.DisplayFormat.FormatString = numberFormat;
        }
        return column;
    }

    private void AddDateColumn(string fieldName, string caption)
    {
        GridColumn column = AddColumn(fieldName, caption, width: 115);
        column.DisplayFormat.FormatType = FormatType.DateTime;
        column.DisplayFormat.FormatString = "dd.MM.yyyy HH:mm";
    }

    // ---------------- Veri ----------------

    private async Task ReloadAsync()
    {
        try
        {
            int? focusedId = FocusedRow?.Id;

            using var db = DbContextFactory.Create();
            List<Trade> trades = await new TradeRepository(db).GetAllWithDetailsAsync();

            _tradesById = trades.ToDictionary(t => t.Id);
            _grid.DataSource = trades.Select(TradeRow.From).ToList();

            if (focusedId is int id)
                _view.FocusedRowHandle = _view.LocateByValue(nameof(TradeRow.Id), id);
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(this, ex);
        }

        UpdateButtonStates();
        UpdateStatistics();
    }

    private TradeRow? FocusedRow => _view.GetFocusedRow() as TradeRow;

    private void UpdateButtonStates()
    {
        TradeRow? row = FocusedRow;
        _editButton.Enabled = row is not null;
        _deleteButton.Enabled = row is not null;
        _closeButton.Enabled = row is { IsClosed: false };
    }

    // Durum çubuğu, o an grid'de görünen (filtrelenmiş) işlemlerin istatistiğini gösterir.
    // Örneğin "Seans = Londra" filtresi verilince sadece Londra işlemlerinin win rate'i görünür.
    private void UpdateStatistics()
    {
        var visibleTrades = new List<Trade>();
        for (int handle = 0; handle < _view.DataRowCount; handle++)
        {
            if (_view.GetRow(handle) is TradeRow row && _tradesById.TryGetValue(row.Id, out Trade? trade))
                visibleTrades.Add(trade);
        }

        int openCount = visibleTrades.Count(t => !t.IsClosed);
        _countLabel.Caption = $"{visibleTrades.Count} işlem ({openCount} açık)";

        TradeStatistics s = _statistics.Calculate(visibleTrades);
        _statsLabel.Caption = s.TotalTrades == 0
            ? "Kapanmış işlem yok"
            : $"Win rate: %{s.WinRate:0.#}   |   Net: {s.NetProfit:n2}   |   " +
              $"Profit factor: {(s.ProfitFactor is decimal pf ? pf.ToString("0.00") : "-")}   |   " +
              $"Ort. R: {(s.AverageR is decimal ar ? ar.ToString("0.00") : "-")}   |   " +
              $"Beklenti: {s.Expectancy:n2}   |   Max DD: {s.MaxDrawdown:n2}";
    }

    // ---------------- İşlem komutları ----------------

    private async Task NewTradeAsync()
    {
        using var form = new TradeEditForm();
        if (form.ShowDialog(this) == DialogResult.OK)
            await ReloadAsync();
    }

    private async Task EditTradeAsync()
    {
        if (FocusedRow is not TradeRow row)
            return;

        using var form = new TradeEditForm(row.Id);
        if (form.ShowDialog(this) == DialogResult.OK)
            await ReloadAsync();
    }

    private async Task CloseTradeAsync()
    {
        if (FocusedRow is not { IsClosed: false } row)
            return;

        using var form = new CloseTradeForm(row.Id);
        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        await ReloadAsync();
        XtraMessageBox.Show(this, form.ResultMessage, "İşlem kapatıldı",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task DeleteTradeAsync()
    {
        if (FocusedRow is not TradeRow row)
            return;
        if (!ErrorHelper.Confirm(this, $"{row.OpenTime:dd.MM.yyyy HH:mm} tarihli {row.Symbol} işlemi silinsin mi?"))
            return;

        using (var db = DbContextFactory.Create())
            await new TradeRepository(db).DeleteAsync(row.Id);

        await ReloadAsync();
    }

    private async Task OpenLookupAsync(XtraForm form)
    {
        using (form)
            form.ShowDialog(this);

        // Hesap/enstrüman adı değişmiş olabilir, listeyi tazele.
        await ReloadAsync();
    }
}

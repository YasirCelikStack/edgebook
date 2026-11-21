using System.ComponentModel;
using DevExpress.Data;
using DevExpress.Utils;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.Data;
using TradingJournal.Data.Repositories;
using TradingJournal.Services.Screenshots;
using TradingJournal.UI.Controls;
using TradingJournal.UI.Forms.Trades;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Models;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Pages;

// İşlem listesi (solda) + seçili işlemin detayı (sağda).
[DesignerCategory("Code")]
public class TradesPage : PageBase
{
    private enum StatusFilter { All, Open, Closed }

    private readonly GridControl _grid = new() { Dock = DockStyle.Fill };
    private readonly GridView _view;
    private readonly TradeDetailPanel _detail = new();
    private readonly FlatButton _allButton = new("Tümü");
    private readonly FlatButton _openButton = new("Açık");
    private readonly FlatButton _closedButton = new("Kapalı");
    private readonly FlatButton _dateChip = new("", Theme.Glyph.Calendar) { Visible = false };

    private Dictionary<int, Trade> _tradesById = new();
    private StatusFilter _status = StatusFilter.All;
    private DateTime? _dayFilter;

    public TradesPage(AppState state) : base(state)
    {
        _view = new GridView(_grid);
        _grid.MainView = _view;
        ConfigureGrid();

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.Background, WrapContents = false };
        toolbar.Controls.AddRange([_allButton, _openButton, _closedButton, _dateChip]);
        _allButton.Click += (_, _) => SetStatus(StatusFilter.All);
        _openButton.Click += (_, _) => SetStatus(StatusFilter.Open);
        _closedButton.Click += (_, _) => SetStatus(StatusFilter.Closed);
        _dateChip.Click += (_, _) => FilterByDay(null);
        SetStatus(StatusFilter.All);

        var listCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        listCard.Controls.Add(_grid);

        _detail.EditRequested += async (_, id) => await EditTradeAsync(id);
        _detail.CloseRequested += async (_, id) => await CloseTradeAsync(id);
        _detail.DeleteRequested += async (_, id) => await DeleteTradeAsync(id);
        _detail.ScreenshotsChanged += async (_, _) => await RefreshAsync();

        Controls.Add(listCard);
        Controls.Add(new Panel { Dock = DockStyle.Right, Width = 12, BackColor = Theme.Background });
        Controls.Add(_detail);
        Controls.Add(toolbar);
    }

    public override string Title => "İşlemler";
    public override string Subtitle => "Bir işleme tıkla; sağda detayları ve chart görüntüleri açılır";

    // ---------------- Liste ----------------

    private void ConfigureGrid()
    {
        _view.OptionsBehavior.Editable = false;
        _view.OptionsBehavior.AutoPopulateColumns = false;
        _view.OptionsView.ShowGroupPanel = false;
        _view.OptionsView.ShowIndicator = false;
        _view.OptionsView.ShowVerticalLines = DefaultBoolean.False;
        _view.OptionsView.ShowFooter = true;
        _view.OptionsView.ColumnAutoWidth = true;
        _view.OptionsFind.AlwaysVisible = true;
        _view.OptionsFind.FindNullPrompt = "Sembol, strateji, etiket ara...";
        _view.OptionsSelection.EnableAppearanceFocusedCell = false;
        _view.RowHeight = 38;
        _view.Appearance.Row.BackColor = Theme.Card;
        _view.Appearance.Empty.BackColor = Theme.Card;
        _view.Appearance.FooterPanel.BackColor = Theme.Card;
        _view.Appearance.HeaderPanel.ForeColor = Theme.TextMuted;

        AddDateColumn(nameof(TradeRow.OpenTime), "Tarih", 110);
        GridColumn symbol = AddColumn(nameof(TradeRow.Symbol), "Sembol", 80);
        symbol.AppearanceCell.Font = Theme.BodyBold;
        AddColumn(nameof(TradeRow.DirectionText), "Yön", 70);
        AddColumn(nameof(TradeRow.Account), "Hesap", 110);
        AddColumn(nameof(TradeRow.Strategy), "Strateji", 120);
        AddColumn(nameof(TradeRow.Session), "Seans", 70);
        AddColumn(nameof(TradeRow.Size), "Boyut", 80);
        GridColumn pnl = AddColumn(nameof(TradeRow.NetPnL), "Net PnL", 85, "+#,0.00;-#,0.00;0.00");
        GridColumn r = AddColumn(nameof(TradeRow.RMultiple), "R", 55, "+0.00;-0.00;0.00");
        AddColumn(nameof(TradeRow.Tags), "Etiketler", 130);
        GridColumn shots = AddColumn(nameof(TradeRow.ScreenshotCount), "SS", 40);
        shots.AppearanceCell.TextOptions.HAlignment = HorzAlignment.Center;
        AddColumn(nameof(TradeRow.Status), "Durum", 60);

        // Saklı sütunlar: filtreleme ve sıralama için.
        _view.Columns[nameof(TradeRow.OpenTime)].SortOrder = ColumnSortOrder.Descending;
        GridColumn closeTime = _view.Columns.AddField(nameof(TradeRow.CloseTime));
        closeTime.Visible = false;
        GridColumn isClosed = _view.Columns.AddField(nameof(TradeRow.IsClosed));
        isClosed.Visible = false;

        symbol.Summary.Add(SummaryItemType.Count, symbol.FieldName, "{0} işlem");
        pnl.Summary.Add(SummaryItemType.Sum, pnl.FieldName, "{0:+#,0.00;-#,0.00;0.00}");
        r.Summary.Add(SummaryItemType.Sum, r.FieldName, "{0:+0.00;-0.00;0.00}R");

        _view.RowCellStyle += (_, e) =>
        {
            if (_view.GetRow(e.RowHandle) is not TradeRow row) return;
            switch (e.Column.FieldName)
            {
                case nameof(TradeRow.NetPnL):
                case nameof(TradeRow.RMultiple):
                    e.Appearance.ForeColor = Theme.ForValue(e.Column.FieldName == nameof(TradeRow.NetPnL) ? row.NetPnL : row.RMultiple);
                    e.Appearance.FontStyleDelta = FontStyle.Bold;
                    break;
                case nameof(TradeRow.DirectionText):
                    e.Appearance.ForeColor = row.Direction == TradeDirection.Long ? Theme.Profit : Theme.Loss;
                    break;
                case nameof(TradeRow.Status):
                    e.Appearance.ForeColor = row.IsClosed ? Theme.TextMuted : Theme.Accent;
                    break;
                case nameof(TradeRow.ScreenshotCount):
                    e.Appearance.ForeColor = row.ScreenshotCount > 0 ? Theme.Accent : Theme.TextFaint;
                    break;
            }
        };

        _view.CustomColumnDisplayText += (_, e) =>
        {
            if (e.Column.FieldName == nameof(TradeRow.ScreenshotCount) && e.Value is int count)
                e.DisplayText = count == 0 ? "—" : count.ToString();
        };

        _view.CustomDrawEmptyForeground += (_, e) =>
        {
            string text = _tradesById.Count == 0
                ? "Henüz işlem yok. Sağ üstteki \"Yeni işlem\" ile başla."
                : "Filtreye uyan işlem yok.";
            e.Appearance.ForeColor = Theme.TextMuted;
            e.Appearance.TextOptions.HAlignment = HorzAlignment.Center;
            e.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            e.Appearance.DrawString(e.Cache, text, e.Bounds);
            e.Handled = true;
        };

        _view.FocusedRowChanged += async (_, _) => await ShowFocusedAsync();
        _view.DoubleClick += async (_, _) => { if (FocusedRow is TradeRow row) await EditTradeAsync(row.Id); };
    }

    private GridColumn AddColumn(string field, string caption, int width, string? format = null)
    {
        GridColumn column = _view.Columns.AddVisible(field, caption);
        column.Width = width;
        if (format is not null)
        {
            column.DisplayFormat.FormatType = FormatType.Numeric;
            column.DisplayFormat.FormatString = format;
        }
        return column;
    }

    private void AddDateColumn(string field, string caption, int width)
    {
        GridColumn column = AddColumn(field, caption, width);
        column.DisplayFormat.FormatType = FormatType.DateTime;
        column.DisplayFormat.FormatString = "dd.MM.yy HH:mm";
    }

    private TradeRow? FocusedRow => _view.GetFocusedRow() as TradeRow;

    public override async Task RefreshAsync()
    {
        int? focusedId = FocusedRow?.Id ?? _detail.TradeId;

        List<Trade> trades = await State.LoadTradesAsync();
        Dictionary<int, int> shotCounts = await State.LoadScreenshotCountsAsync();
        _tradesById = trades.ToDictionary(t => t.Id);

        _grid.DataSource = trades.Select(t =>
        {
            TradeRow row = TradeRow.From(t);
            row.ScreenshotCount = shotCounts.GetValueOrDefault(t.Id);
            return row;
        }).ToList();

        if (focusedId is int id)
            SelectTrade(id);
        await ShowFocusedAsync();
    }

    public void SelectTrade(int tradeId)
    {
        int handle = _view.LocateByValue(nameof(TradeRow.Id), tradeId);
        if (handle >= 0)
            _view.FocusedRowHandle = handle;
    }

    private async Task ShowFocusedAsync()
    {
        Trade? trade = FocusedRow is TradeRow row ? _tradesById.GetValueOrDefault(row.Id) : null;
        await _detail.ShowTradeAsync(trade);
    }

    // ---------------- Filtreler ----------------

    private void SetStatus(StatusFilter status)
    {
        _status = status;
        _allButton.Primary = status == StatusFilter.All;
        _openButton.Primary = status == StatusFilter.Open;
        _closedButton.Primary = status == StatusFilter.Closed;
        _allButton.Invalidate(); _openButton.Invalidate(); _closedButton.Invalidate();
        ApplyFilter();
    }

    // Takvimden bir gün seçilince o gün kapanan işlemler gösterilir. null = filtre yok.
    public void FilterByDay(DateTime? day)
    {
        _dayFilter = day?.Date;
        _dateChip.Visible = day is not null;
        if (day is DateTime d)
            _dateChip.Text = $"{d:dd.MM.yyyy}  ✕";
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var parts = new List<string>();
        if (_status == StatusFilter.Open) parts.Add("[IsClosed] = False");
        if (_status == StatusFilter.Closed) parts.Add("[IsClosed] = True");
        if (_dayFilter is DateTime d)
            parts.Add($"[CloseTime] >= #{d:yyyy-MM-dd}# And [CloseTime] < #{d.AddDays(1):yyyy-MM-dd}#");
        _view.ActiveFilterString = string.Join(" And ", parts);
    }

    // Ctrl+V: panoda resim varsa seçili işleme ekran görüntüsü olarak ekle.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.V) && _detail.TradeId is not null && ClipboardHasImage())
        {
            _ = _detail.TryPasteFromClipboardAsync();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static bool ClipboardHasImage()
    {
        if (Clipboard.ContainsImage()) return true;
        return Clipboard.ContainsFileDropList()
               && Clipboard.GetFileDropList().Cast<string>().Any(ScreenshotService.IsSupportedImage);
    }

    // ---------------- İşlem komutları ----------------

    private async Task EditTradeAsync(int id)
    {
        using var form = new TradeEditForm(id);
        if (form.ShowDialog(FindForm()) == DialogResult.OK)
            await RefreshAsync();
    }

    private async Task CloseTradeAsync(int id)
    {
        using var form = new CloseTradeForm(id);
        if (form.ShowDialog(FindForm()) != DialogResult.OK)
            return;
        await RefreshAsync();
        DevExpress.XtraEditors.XtraMessageBox.Show(FindForm(), form.ResultMessage, "İşlem kapatıldı",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task DeleteTradeAsync(int id)
    {
        if (!_tradesById.TryGetValue(id, out Trade? trade)) return;
        if (!ErrorHelper.Confirm(FindForm()!, $"{trade.OpenTime:dd.MM.yyyy HH:mm} tarihli {trade.Instrument.Symbol} işlemi ve ekran görüntüleri silinsin mi?"))
            return;

        try
        {
            using (var db = DbContextFactory.Create())
                await new TradeRepository(db).DeleteAsync(id);
            ScreenshotService.DeleteTradeFolder(id);
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
        }
        await RefreshAsync();
    }
}

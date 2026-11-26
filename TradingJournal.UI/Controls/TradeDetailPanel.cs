using System.ComponentModel;
using System.Drawing.Imaging;
using DevExpress.XtraEditors;
using TradingJournal.Core.Entities;
using TradingJournal.Core.Enums;
using TradingJournal.Data;
using TradingJournal.Data.Repositories;
using TradingJournal.Services.Screenshots;
using TradingJournal.Services.Statistics;
using TradingJournal.UI.Forms;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Controls;

// İşlemler sayfasının sağındaki detay paneli: sonuç, bilgiler, etiketler,
// ekran görüntüleri ve notlar. Düzenle/Kapat/Sil istekleri sayfaya olay olarak iletilir.
[DesignerCategory("Code")]
public class TradeDetailPanel : Panel
{
    private readonly ScreenshotService _screenshots = new();

    private readonly FlowLayoutPanel _stack;
    private readonly Label _emptyLabel;
    private readonly HeaderView _header = new();
    private readonly CardPanel _infoCard = new("Detaylar");
    private readonly KeyValueView _info = new();
    private readonly CardPanel _tagsCard = new("Etiketler");
    private readonly FlowLayoutPanel _tagFlow = new() { Dock = DockStyle.Fill, BackColor = Theme.Card };
    private readonly CardPanel _shotsCard = new("Ekran görüntüleri") { Hint = "Ctrl+V ile yapıştır" };
    private readonly ScreenshotStrip _strip = new() { Dock = DockStyle.Fill };
    private readonly CardPanel _notesCard = new("Notlar");
    private readonly MemoEdit _notes = new() { Dock = DockStyle.Fill };
    private readonly FlatButton _saveNotes = new("Notu kaydet", Theme.Glyph.Save, primary: true) { Visible = false };
    private readonly FlowLayoutPanel _actions = new() { Height = 48, BackColor = Theme.Background };
    private readonly FlatButton _editButton = new("Düzenle", Theme.Glyph.Edit);
    private readonly FlatButton _closeButton = new("Kapat", Theme.Glyph.Check, primary: true);
    private readonly FlatButton _deleteButton = new("Sil", Theme.Glyph.Delete) { Danger = true };

    private Trade? _trade;
    private bool _loadingNotes;

    public event EventHandler<int>? EditRequested;
    public event EventHandler<int>? CloseRequested;
    public event EventHandler<int>? DeleteRequested;
    public event EventHandler? ScreenshotsChanged;

    public TradeDetailPanel()
    {
        Dock = DockStyle.Right;
        Width = 420;
        BackColor = Theme.Background;
        Padding = new Padding(4, 0, 0, 0);
        AllowDrop = true;

        _stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Background,
            Visible = false
        };

        _emptyLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Detayları görmek için\nlisteden bir işlem seç",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.TextMuted,
            Font = Theme.Body,
            BackColor = Theme.Background
        };

        _infoCard.Controls.Add(_info);
        _info.Dock = DockStyle.Fill;
        _tagsCard.Controls.Add(_tagFlow);
        _shotsCard.Controls.Add(_strip);

        _notes.Properties.MaxLength = 4000;
        _notes.EditValueChanged += (_, _) => _saveNotes.Visible = !_loadingNotes;
        _saveNotes.Dock = DockStyle.Bottom;
        _saveNotes.Click += async (_, _) => await SaveNotesAsync();
        _notesCard.Controls.Add(_notes);
        _notesCard.Controls.Add(_saveNotes);

        _actions.Controls.Add(_closeButton);
        _actions.Controls.Add(_editButton);
        _actions.Controls.Add(_deleteButton);
        _editButton.Click += (_, _) => { if (_trade is not null) EditRequested?.Invoke(this, _trade.Id); };
        _closeButton.Click += (_, _) => { if (_trade is not null) CloseRequested?.Invoke(this, _trade.Id); };
        _deleteButton.Click += (_, _) => { if (_trade is not null) DeleteRequested?.Invoke(this, _trade.Id); };

        _strip.AddRequested += async (_, _) => await BrowseAndAddAsync();
        _strip.OpenRequested += (_, shot) => OpenScreenshot(shot);
        _strip.DeleteRequested += async (_, shot) => await DeleteScreenshotAsync(shot);

        _stack.Controls.AddRange([_header, _infoCard, _tagsCard, _shotsCard, _notesCard, _actions]);
        _stack.Resize += (_, _) => LayoutCards();

        Controls.Add(_stack);
        Controls.Add(_emptyLabel);
    }

    public int? TradeId => _trade?.Id;

    public async Task ShowTradeAsync(Trade? trade)
    {
        _trade = trade;
        _stack.Visible = trade is not null;
        _emptyLabel.Visible = trade is null;
        if (trade is null)
            return;

        _header.SetTrade(trade);
        _info.SetRows(BuildInfoRows(trade));

        _tagFlow.Controls.Clear();
        if (trade.Tags.Count == 0)
            _tagFlow.Controls.Add(new Label { Text = "Etiket yok", ForeColor = Theme.TextFaint, AutoSize = true, Font = Theme.Small });
        foreach (Tag tag in trade.Tags.OrderBy(t => t.Name))
            _tagFlow.Controls.Add(new Chip(tag.Name, ColorFromHex(tag.ColorHex)));

        _loadingNotes = true;
        _notes.Text = trade.Notes ?? string.Empty;
        _loadingNotes = false;
        _saveNotes.Visible = false;

        _closeButton.Visible = !trade.IsClosed;

        await ReloadScreenshotsAsync();
    }

    // ---------------- Ekran görüntüleri ----------------

    private async Task ReloadScreenshotsAsync()
    {
        if (_trade is null) return;
        List<TradeScreenshot> list = await _screenshots.GetForTradeAsync(_trade.Id);
        _strip.SetScreenshots(list);
        LayoutCards();
    }

    private async Task BrowseAndAddAsync()
    {
        if (_trade is null) return;
        using var dialog = new OpenFileDialog
        {
            Title = "Chart ekran görüntüsü seç",
            Filter = "Resimler|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await AddFilesAsync(dialog.FileNames);
    }

    public async Task AddFilesAsync(IEnumerable<string> paths)
    {
        if (_trade is null) return;
        try
        {
            foreach (string path in paths.Where(ScreenshotService.IsSupportedImage))
                await _screenshots.AddFromFileAsync(_trade.Id, path);
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
        }
        await ReloadScreenshotsAsync();
        ScreenshotsChanged?.Invoke(this, EventArgs.Empty);
    }

    // Panodaki resmi (Snipping Tool, Print Screen, TradingView "Copy chart image") ekler.
    // Panoda resim yoksa false döner; böylece Ctrl+V normal yapıştırma olarak çalışmaya devam eder.
    public async Task<bool> TryPasteFromClipboardAsync()
    {
        if (_trade is null)
            return false;

        if (Clipboard.ContainsFileDropList())
        {
            var files = Clipboard.GetFileDropList().Cast<string>().Where(ScreenshotService.IsSupportedImage).ToList();
            if (files.Count == 0) return false;
            await AddFilesAsync(files);
            return true;
        }

        if (!Clipboard.ContainsImage())
            return false;

        try
        {
            using Image? image = Clipboard.GetImage();
            if (image is null) return false;
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            await _screenshots.AddFromStreamAsync(_trade.Id, stream, ".png");
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
        }
        await ReloadScreenshotsAsync();
        ScreenshotsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void OpenScreenshot(TradeScreenshot shot)
    {
        string path = ScreenshotService.GetFullPath(shot);
        if (!File.Exists(path))
        {
            XtraMessageBox.Show(FindForm(), "Resim dosyası bulunamadı.", "EdgeBook", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        using var viewer = new ImageViewerForm(path, $"{_trade?.Instrument.Symbol} · {shot.CreatedAt:dd.MM.yyyy HH:mm}");
        viewer.ShowDialog(FindForm());
    }

    private async Task DeleteScreenshotAsync(TradeScreenshot shot)
    {
        if (!ErrorHelper.Confirm(FindForm()!, "Bu ekran görüntüsü silinsin mi?"))
            return;
        try
        {
            await _screenshots.DeleteAsync(shot);
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
        }
        await ReloadScreenshotsAsync();
        ScreenshotsChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        if (_trade is not null && e.Data?.GetData(DataFormats.FileDrop) is string[] files
            && files.Any(ScreenshotService.IsSupportedImage))
            e.Effect = DragDropEffects.Copy;
    }

    protected override async void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
            await AddFilesAsync(files);
    }

    // ---------------- Notlar ----------------

    private async Task SaveNotesAsync()
    {
        if (_trade is null) return;
        try
        {
            using var db = DbContextFactory.Create();
            var repository = new TradeRepository(db);
            Trade? trade = await repository.GetByIdAsync(_trade.Id);
            if (trade is null) return;
            trade.Notes = string.IsNullOrWhiteSpace(_notes.Text) ? null : _notes.Text.Trim();
            await repository.UpdateAsync(trade);
            _trade.Notes = trade.Notes;
            _saveNotes.Visible = false;
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
        }
    }

    // ---------------- Yerleşim ----------------

    // FlowLayoutPanel çocukların genişliğini kendisi ayarlamaz; panel genişledikçe biz ayarlarız.
    private void LayoutCards()
    {
        int width = _stack.ClientSize.Width - 16;
        if (width <= 0) return;

        _header.Size = new Size(width, 172);
        _infoCard.Size = new Size(width, 44 + _info.PreferredHeight + 12);
        _tagsCard.Size = new Size(width, 100);
        _shotsCard.Size = new Size(width, 44 + _strip.GetPreferredHeight(width - 28) + 6);
        _notesCard.Size = new Size(width, 200);
        _actions.Size = new Size(width, 48);
    }

    private static List<(string Key, string Value, Color? Color)> BuildInfoRows(Trade trade)
    {
        string size = trade switch
        {
            ForexTrade f => $"{f.LotSize:0.####} lot",
            CryptoTrade c => $"{c.Quantity:0.####} (x{c.Leverage})",
            _ => "-"
        };
        string duration = trade.CloseTime is DateTime close ? FormatDuration(close - trade.OpenTime) : "Devam ediyor";

        // Kontrol listesi: "3/4 kural" (yeşil: hepsine uyuldu, turuncu: en az biri çiğnendi).
        int met = trade.RuleChecks.Count(c => c.IsMet);
        (string checklist, Color? checklistColor) = trade.RuleChecks.Count == 0
            ? ("-", (Color?)null)
            : ($"{met}/{trade.RuleChecks.Count} kurala uyuldu", met == trade.RuleChecks.Count ? Theme.Profit : Theme.Warning);

        return
        [
            ("Giriş", trade.EntryPrice.ToString("0.#####"), null),
            ("Stop", trade.StopLoss.ToString("0.#####"), Theme.Loss),
            ("Hedef", trade.TakeProfit?.ToString("0.#####") ?? "-", trade.TakeProfit is null ? null : Theme.Profit),
            ("Çıkış", trade.ExitPrice?.ToString("0.#####") ?? "-", null),
            ("Boyut", size, null),
            ("Risk", $"{trade.CalculateRiskAmount():n2} {trade.Account.Currency}", null),
            ("Seans", SessionClassifier.GetSession(trade.OpenTime).ToTurkish(), null),
            ("Süre", duration, null),
            ("Strateji", trade.Strategy?.Name ?? "-", null),
            ("Kontrol listesi", checklist, checklistColor),
            ("Puan", trade.Rating is int r ? new string('★', r) + new string('☆', 5 - r) : "-", Theme.Warning),
        ];
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays} g {span.Hours} sa";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours} sa {span.Minutes} dk";
        return $"{Math.Max(0, (int)span.TotalMinutes)} dk";
    }

    private static Color ColorFromHex(string? hex)
        => ColorParser.TryParse(hex, out Color color) ? color : Theme.Accent;

    // ---------------- İç kontroller ----------------

    // Üstteki büyük kart: sembol, yön, durum, sonuç.
    [DesignerCategory("Code")]
    private class HeaderView : Control
    {
        private Trade? _trade;

        public HeaderView()
        {
            DoubleBuffered = true;
            BackColor = Theme.Background;
            Margin = new Padding(0, 0, 0, 8);
        }

        public void SetTrade(Trade trade)
        {
            _trade = trade;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_trade is null) return;
            Graphics g = e.Graphics;
            Theme.FillRounded(g, Theme.Card, new RectangleF(0, 0, Width - 1, Height - 1), 12);

            using (var symbolFont = new Font(Theme.FontName, 18f, FontStyle.Bold))
                TextRenderer.DrawText(g, _trade.Instrument.Symbol, symbolFont, new Point(14, 12), Theme.Text);

            bool isLong = _trade.Direction == TradeDirection.Long;
            DrawPill(g, new Point(16, 50), isLong ? "▲ LONG" : "▼ SHORT", isLong ? Theme.Profit : Theme.Loss);
            DrawPill(g, new Point(108, 50), _trade.IsClosed ? "KAPALI" : "AÇIK", _trade.IsClosed ? Theme.TextMuted : Theme.Accent);

            TextRenderer.DrawText(g, $"{_trade.Account.Name} · {_trade.OpenTime:dd MMM yyyy HH:mm}", Theme.Small,
                new Point(16, 84), Theme.TextMuted);

            // Sağ tarafta sonuç
            decimal? pnl = _trade.NetPnL;
            string pnlText = pnl is decimal p ? p.ToString("+#,0.00;-#,0.00;0.00") : "—";
            string rText = _trade.RMultiple is decimal r ? $"{r:+0.00;-0.00;0.00} R" : "Açık işlem";
            var right = new Rectangle(0, 14, Width - 16, 34);
            TextRenderer.DrawText(g, pnlText, Theme.BigNumber, right, Theme.ForValue(pnl), TextFormatFlags.Right);
            TextRenderer.DrawText(g, rText, Theme.BodyBold, new Rectangle(0, 50, Width - 18, 22), Theme.ForValue(_trade.RMultiple),
                TextFormatFlags.Right);
            TextRenderer.DrawText(g, _trade.Account.Currency, Theme.Small, new Rectangle(0, 74, Width - 18, 18), Theme.TextFaint,
                TextFormatFlags.Right);

            // Altta risk/ödül çubuğu: stop'tan hedefe, girişin yeri işaretli.
            DrawRiskBar(g, new Rectangle(16, Height - 24, Width - 32, 8));
        }

        private static void DrawPill(Graphics g, Point location, string text, Color color)
        {
            Size size = TextRenderer.MeasureText(text, Theme.SmallBold);
            var rect = new RectangleF(location.X, location.Y, size.Width + 16, 24);
            Theme.FillRounded(g, Theme.Tint(color, 0.22), rect, 11);
            TextRenderer.DrawText(g, text, Theme.SmallBold, Rectangle.Round(rect), ControlPaint.Light(color, 0.3f),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawRiskBar(Graphics g, Rectangle bar)
        {
            if (_trade?.TakeProfit is not decimal tp) 
            {
                TextRenderer.DrawText(g, "Hedef girilmemiş", Theme.Small, new Point(bar.X, bar.Y - 14), Theme.TextFaint);
                return;
            }

            decimal risk = Math.Abs(_trade.EntryPrice - _trade.StopLoss);
            decimal reward = Math.Abs(tp - _trade.EntryPrice);
            if (risk + reward == 0) return;

            int riskWidth = (int)(bar.Width * (double)(risk / (risk + reward)));
            Theme.FillRounded(g, Theme.Tint(Theme.Loss, 0.7), new RectangleF(bar.X, bar.Y, riskWidth, bar.Height), 4);
            Theme.FillRounded(g, Theme.Tint(Theme.Profit, 0.7), new RectangleF(bar.X + riskWidth, bar.Y, bar.Width - riskWidth, bar.Height), 4);

            string rr = risk == 0 ? "-" : $"1 : {reward / risk:0.0#}";
            TextRenderer.DrawText(g, $"Risk/ödül {rr}", Theme.Small, new Point(bar.X, bar.Y - 20), Theme.TextMuted);
        }
    }

    // İki sütunlu "etiket: değer" listesi.
    [DesignerCategory("Code")]
    private class KeyValueView : Control
    {
        private const int RowHeight = 42;
        private List<(string Key, string Value, Color? Color)> _rows = new();

        public KeyValueView()
        {
            DoubleBuffered = true;
            BackColor = Theme.Card;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        public int PreferredHeight => (int)Math.Ceiling(_rows.Count / 2.0) * RowHeight;

        public void SetRows(List<(string Key, string Value, Color? Color)> rows)
        {
            _rows = rows;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int columnWidth = Width / 2;
            for (int i = 0; i < _rows.Count; i++)
            {
                (string key, string value, Color? color) = _rows[i];
                int x = (i % 2) * columnWidth;
                int y = (i / 2) * RowHeight;
                TextRenderer.DrawText(e.Graphics, key, Theme.Small, new Point(x, y), Theme.TextMuted);
                TextRenderer.DrawText(e.Graphics, value, Theme.BodyBold,
                    new Rectangle(x, y + 16, columnWidth - 8, 22), color ?? Theme.Text, TextFormatFlags.EndEllipsis);
            }
        }
    }
}

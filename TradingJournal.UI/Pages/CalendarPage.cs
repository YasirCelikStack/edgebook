using System.ComponentModel;
using System.Globalization;
using DevExpress.XtraEditors;
using TradingJournal.Core.Entities;
using TradingJournal.Services.Notes;
using TradingJournal.UI.Controls;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Pages;

// Aylık PnL takvimi + seçili günün notu. İşlemler kapanış gününe göre toplanır.
// Bir güne tıklayınca sağda o günün planı ve değerlendirmesi açılır; çift tıklayınca işlemleri listelenir.
[DesignerCategory("Code")]
public class CalendarPage : PageBase
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private readonly DailyNoteService _notes = new();
    private readonly PnlCalendar _calendar = new() { Dock = DockStyle.Fill };
    private readonly Label _monthLabel = new() { AutoSize = true, Font = Theme.Title, ForeColor = Theme.Text, Margin = new Padding(12, 4, 12, 0) };
    private readonly Label _summaryLabel = new() { AutoSize = true, Font = Theme.Body, ForeColor = Theme.TextMuted, Margin = new Padding(16, 12, 0, 0) };
    private List<Trade> _trades = new();

    // Not paneli
    private readonly CardPanel _noteCard = new("Günlük not") { Dock = DockStyle.Right, Width = 380 };
    private readonly Label _dayLabel = new() { AutoSize = true, Font = Theme.Heading, ForeColor = Theme.Text, Margin = new Padding(0, 0, 0, 2) };
    private readonly Label _dayResult = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextMuted, Margin = new Padding(0, 0, 0, 10) };
    private readonly MemoEdit _plan = new();
    private readonly MemoEdit _review = new();
    private readonly RatingControl _mood = new();
    private readonly Label _moodText = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextMuted, Margin = new Padding(8, 6, 0, 0) };
    private readonly FlatButton _saveButton = new("Kaydet", Theme.Glyph.Save, primary: true);
    private readonly FlatButton _tradesButton = new("İşlemleri gör", Theme.Glyph.List);
    private readonly Label _saveState = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextFaint, Margin = new Padding(8, 12, 0, 0) };

    private DateTime _selectedDay = DateTime.Today;
    private bool _dirty;
    private bool _loadingNote;
    private bool _saving;

    // Ana pencere İşlemler sayfasını o güne filtreli açar.
    public event EventHandler<DateTime>? DaySelected;

    public CalendarPage(AppState state) : base(state)
    {
        var prev = new FlatButton("", Theme.Glyph.Back) { Width = 40 };
        var next = new FlatButton("", Theme.Glyph.Forward) { Width = 40 };
        var today = new FlatButton("Bugün");
        prev.Click += async (_, _) => await ChangeMonthAsync(-1);
        next.Click += async (_, _) => await ChangeMonthAsync(1);
        today.Click += async (_, _) =>
        {
            _calendar.Month = DateTime.Today;
            await UpdateCalendarAsync();
            await SelectDayAsync(DateTime.Today);
        };

        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.Background, WrapContents = false };
        header.Controls.AddRange([prev, _monthLabel, next, today, _summaryLabel]);

        var card = new CardPanel { Dock = DockStyle.Fill };
        card.Controls.Add(_calendar);

        _calendar.DaySelected += async (_, day) => await SelectDayAsync(day);
        _calendar.DayClicked += (_, day) => DaySelected?.Invoke(this, day);

        BuildNotePanel();

        // Sayfadan ayrılırken kaydedilmemiş not kaybolmasın.
        // (Ana pencere sayfa değiştirirken sayfayı içerikten çıkarır: ParentChanged.)
        VisibleChanged += async (_, _) =>
        {
            if (!Visible && _dirty)
                await SaveNoteAsync(showErrors: false);
        };
        ParentChanged += async (_, _) =>
        {
            if (Parent is null && _dirty)
                await SaveNoteAsync(showErrors: false);
        };

        // Takvim ile not paneli arasında boşluk.
        var spacer = new Panel { Dock = DockStyle.Right, Width = 12, BackColor = Theme.Background };
        Controls.Add(card);
        Controls.Add(spacer);
        Controls.Add(_noteCard);
        Controls.Add(header);
    }

    public override string Title => "Takvim";
    public override string Subtitle => "Bir güne tıkla: planını ve değerlendirmeni yaz. Çift tıkla: o günün işlemleri.";

    public override async Task RefreshAsync()
    {
        _trades = await State.LoadTradesAsync();
        await UpdateCalendarAsync();
        await SelectDayAsync(_selectedDay);
    }

    private async Task ChangeMonthAsync(int delta)
    {
        _calendar.Month = _calendar.Month.AddMonths(delta);
        await UpdateCalendarAsync();
    }

    private async Task UpdateCalendarAsync()
    {
        var days = _trades
            .Where(t => t.IsClosed)
            .GroupBy(t => t.CloseTime!.Value.Date)
            .ToDictionary(g => g.Key, g => new PnlCalendar.DaySummary(g.Sum(t => t.NetPnL!.Value), g.Count()));
        _calendar.SetData(days);

        DateTime month = _calendar.Month;
        _monthLabel.Text = month.ToString("MMMM yyyy", Turkish);

        var monthDays = days.Where(d => d.Key.Year == month.Year && d.Key.Month == month.Month).Select(d => d.Value).ToList();
        decimal net = monthDays.Sum(d => d.NetPnL);
        int green = monthDays.Count(d => d.NetPnL > 0);
        int red = monthDays.Count(d => d.NetPnL < 0);
        _summaryLabel.Text = monthDays.Count == 0
            ? "Bu ay kapanmış işlem yok"
            : $"Ay toplamı: {net:+#,0.00;-#,0.00;0}   ·   {green} yeşil gün, {red} kırmızı gün";
        _summaryLabel.ForeColor = monthDays.Count == 0 ? Theme.TextMuted : Theme.ForValue(net);

        // Görünen 6 haftalık aralıktaki notlu günler.
        try
        {
            List<DailyNote> notes = await _notes.GetRangeAsync(month.AddDays(-7), month.AddMonths(1).AddDays(14));
            _calendar.SetNoteDays(notes.Select(n => n.Date));
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
        }
    }

    // ---------------- Not paneli ----------------

    private void BuildNotePanel()
    {
        _plan.Properties.MaxLength = 4000;
        _review.Properties.MaxLength = 4000;
        _plan.Properties.NullValuePrompt = "Bias, izlediğin seviyeler, haber saatleri, bugünkü kuralın...";
        _review.Properties.NullValuePrompt = "Plana uydun mu? Ne iyi gitti, ne kötü gitti? Yarın neyi farklı yapacaksın?";
        _mood.Properties.ItemCount = 5;

        _plan.EditValueChanged += (_, _) => MarkDirty();
        _review.EditValueChanged += (_, _) => MarkDirty();
        _mood.EditValueChanged += (_, _) => { UpdateMoodText(); MarkDirty(); };
        _saveButton.Click += async (_, _) => await SaveNoteAsync(showErrors: true);
        _tradesButton.Click += (_, _) => DaySelected?.Invoke(this, _selectedDay);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            BackColor = Theme.Card
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // tarih
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // günün sonucu
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // "Seans öncesi plan"
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));   // plan
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // "Gün sonu değerlendirmesi"
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));   // değerlendirme
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // "Duygu durumu"
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // yıldızlar
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // düğmeler

        Label Caption(string text) => new()
        {
            Text = text, AutoSize = true, Font = Theme.SmallBold, ForeColor = Theme.TextMuted, Margin = new Padding(0, 8, 0, 4)
        };

        _plan.Dock = DockStyle.Fill;
        _review.Dock = DockStyle.Fill;

        var moodRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Card, Margin = Padding.Empty };
        _mood.Width = 130;
        moodRow.Controls.Add(_mood);
        moodRow.Controls.Add(_moodText);

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Card, Margin = new Padding(0, 10, 0, 0) };
        _saveButton.Margin = new Padding(0, 0, 8, 0);
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(_tradesButton);
        buttons.Controls.Add(_saveState);

        layout.Controls.Add(_dayLabel, 0, 0);
        layout.Controls.Add(_dayResult, 0, 1);
        layout.Controls.Add(Caption("SEANS ÖNCESİ PLAN"), 0, 2);
        layout.Controls.Add(_plan, 0, 3);
        layout.Controls.Add(Caption("GÜN SONU DEĞERLENDİRMESİ"), 0, 4);
        layout.Controls.Add(_review, 0, 5);
        layout.Controls.Add(Caption("DUYGU DURUMU / ZİHİNSEL HAZIRLIK"), 0, 6);
        layout.Controls.Add(moodRow, 0, 7);
        layout.Controls.Add(buttons, 0, 8);
        _noteCard.Controls.Add(layout);
    }

    private async Task SelectDayAsync(DateTime day)
    {
        // Başka güne geçerken (veya sayfa yenilenirken) kaydedilmemiş değişiklik varsa önce kaydet.
        if (_dirty)
            await SaveNoteAsync(showErrors: true);

        _selectedDay = day.Date;
        _calendar.SelectedDay = _selectedDay;
        _dayLabel.Text = _selectedDay.ToString("d MMMM yyyy, dddd", Turkish);

        List<Trade> dayTrades = _trades.Where(t => t.IsClosed && t.CloseTime!.Value.Date == _selectedDay).ToList();
        decimal net = dayTrades.Sum(t => t.NetPnL!.Value);
        _dayResult.Text = dayTrades.Count == 0
            ? "Bu gün kapanan işlem yok"
            : $"{dayTrades.Count} işlem  ·  {net:+#,0.00;-#,0.00;0}";
        _dayResult.ForeColor = dayTrades.Count == 0 ? Theme.TextMuted : Theme.ForValue(net);
        _tradesButton.Enabled = dayTrades.Count > 0;
        _tradesButton.Text = dayTrades.Count > 0 ? $"İşlemleri gör ({dayTrades.Count})" : "İşlem yok";

        DailyNote? note = null;
        try
        {
            note = await _notes.GetAsync(_selectedDay);
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(FindForm(), ex);
        }

        _loadingNote = true;
        _plan.Text = note?.Plan ?? "";
        _review.Text = note?.Review ?? "";
        _mood.EditValue = (decimal)(note?.Mood ?? 0);
        _loadingNote = false;
        UpdateMoodText();

        _dirty = false;
        _saveState.Text = note is null ? "" : $"Son kayıt: {note.UpdatedAt:dd.MM HH:mm}";
    }

    private void MarkDirty()
    {
        if (_loadingNote) return;
        _dirty = true;
        _saveState.Text = "Kaydedilmedi";
    }

    private void UpdateMoodText()
    {
        int mood = (int)Math.Round(Convert.ToDecimal(_mood.EditValue ?? 0m));
        _moodText.Text = mood switch
        {
            1 => "Çok kötü",
            2 => "Kötü",
            3 => "Normal",
            4 => "İyi",
            5 => "Çok iyi, odaklı",
            _ => "Seçilmedi"
        };
    }

    private async Task SaveNoteAsync(bool showErrors)
    {
        if (_saving) return; // Aynı anda iki kayıt (ör. sayfadan çıkarken iki olay birden) olmasın.
        _saving = true;
        int mood = (int)Math.Round(Convert.ToDecimal(_mood.EditValue ?? 0m));
        try
        {
            await _notes.SaveAsync(_selectedDay, _plan.Text, _review.Text, mood > 0 ? mood : null);
            _dirty = false;
            _saveState.Text = $"Kaydedildi {DateTime.Now:HH:mm}";
            await UpdateCalendarAsync();
        }
        catch (Exception ex)
        {
            if (showErrors) ErrorHelper.Show(FindForm(), ex);
        }
        finally
        {
            _saving = false;
        }
    }
}

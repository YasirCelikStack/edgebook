using System.ComponentModel;
using DevExpress.XtraEditors;
using TradingJournal.Core.Entities;
using TradingJournal.Services.Ai;
using TradingJournal.Services.Configuration;
using TradingJournal.UI.Controls;
using TradingJournal.UI.Forms;
using TradingJournal.UI.Helpers;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Pages;

// Yapay zeka koçu: solda haftalık koç raporu, sağda işlemlerin hakkında soru-cevap.
// Gönderilen veri: hazır hesaplanmış istatistikler + son 40 işlemin özeti (TradingContextBuilder).
[DesignerCategory("Code")]
public class CoachPage : PageBase
{
    private static readonly string[] SuggestedQuestions =
    [
        "Hangi seansta ve yönde en çok kaybediyorum?",
        "Kazananlarımı erken mi kapatıyorum?",
        "En güvenebileceğim kurulum hangisi?",
        "Risk yönetimim nasıl, nereyi düzeltmeliyim?",
        "Etiketlerime göre duygusal hatalarım neler?"
    ];

    private readonly Label _keyStatus = new() { AutoSize = true, Font = Theme.Body, ForeColor = Theme.TextMuted, Margin = new Padding(4, 10, 12, 0) };
    private readonly FlatButton _keyButton = new("Anahtar ayarla", "");

    private readonly CardPanel _reportCard = new("Koç raporu");
    private readonly RichTextBox _reportText = CreateTextBox();
    private readonly FlatButton _reportButton = new("Rapor oluştur", "", primary: true);

    private readonly CardPanel _chatCard = new("Koça sor") { Hint = "her soru 1 istek" };
    private readonly RichTextBox _chatText = CreateTextBox();
    private readonly TextEdit _question = new();
    private readonly FlatButton _askButton = new("Sor", "", primary: true);

    private readonly List<ChatMessage> _history = new();
    private List<Trade> _trades = new();
    private bool _busy;

    public CoachPage(AppState state) : base(state)
    {
        _keyButton.Click += async (_, _) => await EditKeyAsync();
        _reportButton.Click += async (_, _) => await CreateReportAsync();
        _askButton.Click += async (_, _) => await AskAsync(_question.Text);
        _question.Properties.NullValuePrompt = "İşlemlerin hakkında bir soru yaz ve Enter'a bas...";
        _question.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            await AskAsync(_question.Text);
        };

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, BackColor = Theme.Background, WrapContents = false };
        top.Controls.Add(_keyStatus);
        top.Controls.Add(_keyButton);

        // Sol: rapor
        var reportBottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, BackColor = Theme.Card, FlowDirection = FlowDirection.RightToLeft };
        reportBottom.Controls.Add(_reportButton);
        _reportCard.Controls.Add(_reportText);
        _reportCard.Controls.Add(reportBottom);

        // Sağ: sohbet
        var suggestions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 92, BackColor = Theme.Card };
        foreach (string q in SuggestedQuestions)
        {
            var chip = new FlatButton(q) { Height = 30, Font = Theme.Small };
            chip.Click += async (_, _) => await AskAsync(q);
            suggestions.Controls.Add(chip);
        }

        var inputRow = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 44, ColumnCount = 2, BackColor = Theme.Card };
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        _question.Dock = DockStyle.Fill;
        _question.Margin = new Padding(0, 6, 8, 0);
        inputRow.Controls.Add(_question, 0, 0);
        inputRow.Controls.Add(_askButton, 1, 0);

        _chatCard.Controls.Add(_chatText);
        _chatCard.Controls.Add(suggestions);
        _chatCard.Controls.Add(inputRow);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        _reportCard.Dock = DockStyle.Fill;
        _chatCard.Dock = DockStyle.Fill;
        root.Controls.Add(_reportCard, 0, 0);
        root.Controls.Add(_chatCard, 1, 0);

        Controls.Add(root);
        Controls.Add(top);

        AppendChat(false, "Merhaba! İşlemlerin hakkında ne merak ediyorsan sor. Aşağıdaki hazır sorulardan birine de tıklayabilirsin.");
    }

    public override string Title => "Yapay zeka koçu";
    public override string Subtitle => "Gemini, istatistiklerini yorumlar: güçlü/zayıf yönler, davranış hataları ve haftalık kurallar";

    // Rapor ve sohbet hangi hesaba ait: hesap değişince ayrı rapor saklanır.
    private string ScopeKey => State.AccountId is int id ? $"account:{id}" : "all";

    public override async Task RefreshAsync()
    {
        _trades = await State.LoadTradesAsync();
        UpdateKeyStatus();
        ShowSavedReport();
    }

    private void UpdateKeyStatus()
    {
        bool hasKey = AiKeyStore.ResolveKey() is not null;
        _keyStatus.Text = (hasKey ? "✓ " : "⚠ ") + AiKeyStore.Describe();
        _keyStatus.ForeColor = hasKey ? Theme.Profit : Theme.Warning;
    }

    private void ShowSavedReport()
    {
        CoachReport? report = CoachReportStore.Load(ScopeKey);
        if (report is null)
        {
            _reportCard.Hint = null;
            SetReportText("Henüz rapor yok.\n\n\"Rapor oluştur\" düğmesine bas; koç işlemlerini inceleyip güçlü ve zayıf yönlerini, " +
                          "davranış hatalarını ve önümüzdeki hafta için 3 kuralı yazsın.\n\nRapor kaydedilir; tekrar açınca kota harcanmaz.",
                Theme.TextMuted);
            _reportButton.Text = "Rapor oluştur";
        }
        else
        {
            _reportCard.Hint = $"{report.CreatedAt:dd.MM.yyyy HH:mm} · {report.Provider}";
            SetReportText(report.Text, Theme.Text);
            _reportButton.Text = "Yeni rapor oluştur";
        }
        _reportCard.Invalidate();
    }

    // ---------------- Komutlar ----------------

    private TradingCoachService CreateCoach()
        => new(new GeminiProvider(AiKeyStore.ResolveKey(), AppSettings.Current.Ai.GeminiModel));

    private string ScopeName => State.AccountId is null
        ? "tüm hesaplar"
        : _trades.FirstOrDefault()?.Account?.Name is string name ? $"\"{name}\" hesabı" : "seçili hesap";

    private async Task CreateReportAsync()
    {
        if (_busy) return;
        await RunBusyAsync(async () =>
        {
            SetReportText("Koç işlemlerini inceliyor...", Theme.TextMuted);
            await CreateCoach().CreateReportAsync(_trades, ScopeKey, ScopeName);
            ShowSavedReport();
        }, onAiError: ex => SetReportText(ex.Message, ErrorColor(ex)));
    }

    private async Task AskAsync(string question)
    {
        if (_busy || string.IsNullOrWhiteSpace(question)) return;
        question = question.Trim();
        _question.Text = string.Empty;
        AppendChat(true, question);

        await RunBusyAsync(async () =>
        {
            AppendChat(false, "Düşünüyor...", Theme.TextFaint);
            string answer = await CreateCoach().AskAsync(_trades, ScopeName, _history, question);
            RemoveLastChatLine();
            AppendChat(false, answer);

            _history.Add(new ChatMessage(true, question));
            _history.Add(new ChatMessage(false, answer));
        }, onAiError: ex =>
        {
            RemoveLastChatLine();
            AppendChat(false, ex.Message, ErrorColor(ex));
        });
    }

    private static Color ErrorColor(AiException ex) => ex is AiQuotaExceededException ? Theme.Warning : Theme.Loss;

    // Kota / anahtar / bağlantı hataları beklenen durumlar: hata kutusu yerine ilgili alanda sakin bir mesaj gösterilir.
    private async Task RunBusyAsync(Func<Task> action, Action<AiException> onAiError)
    {
        _busy = true;
        _reportButton.Enabled = _askButton.Enabled = false;
        try
        {
            await action();
        }
        catch (AiException ex)
        {
            onAiError(ex);
            if (ex is AiAuthenticationException)
                UpdateKeyStatus();
        }
        catch (Exception ex)
        {
            onAiError(new AiException("Beklenmeyen bir hata oluştu."));
            ErrorHelper.Show(FindForm(), ex);
        }
        finally
        {
            _busy = false;
            _reportButton.Enabled = _askButton.Enabled = true;
        }
    }

    private async Task EditKeyAsync()
    {
        using var form = new AiKeyForm();
        if (form.ShowDialog(FindForm()) == DialogResult.OK)
            UpdateKeyStatus();
        await Task.CompletedTask;
    }

    // ---------------- Metin alanları ----------------

    private static RichTextBox CreateTextBox() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Card,
        ForeColor = Theme.Text,
        Font = new Font(Theme.FontName, 10.5f),
        DetectUrls = false,
        ScrollBars = RichTextBoxScrollBars.Vertical
    };

    private void SetReportText(string text, Color color)
    {
        _reportText.Clear();
        _reportText.SelectionColor = color;
        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            // BÜYÜK HARFLE yazılmış satırları başlık olarak vurgula.
            bool isHeading = trimmed.Length > 3 && trimmed == trimmed.ToUpper(new System.Globalization.CultureInfo("tr-TR"))
                             && trimmed.Any(char.IsLetter);
            _reportText.SelectionFont = isHeading ? new Font(Theme.FontName, 11f, FontStyle.Bold) : _reportText.Font;
            _reportText.SelectionColor = isHeading ? Theme.Accent : color;
            _reportText.AppendText((isHeading ? "\n" : "") + trimmed + "\n");
        }
        _reportText.SelectionStart = 0;
        _reportText.ScrollToCaret();
    }

    private int _lastLineStart = -1;

    private void AppendChat(bool fromUser, string text, Color? color = null)
    {
        _lastLineStart = _chatText.TextLength;
        _chatText.SelectionStart = _chatText.TextLength;
        _chatText.SelectionFont = new Font(Theme.FontName, 9f, FontStyle.Bold);
        _chatText.SelectionColor = fromUser ? Theme.Accent : Theme.Profit;
        _chatText.AppendText(fromUser ? "Sen\n" : "Koç\n");
        _chatText.SelectionFont = _chatText.Font;
        _chatText.SelectionColor = color ?? Theme.Text;
        _chatText.AppendText(text.Trim() + "\n\n");
        _chatText.SelectionStart = _chatText.TextLength;
        _chatText.ScrollToCaret();
    }

    // "Düşünüyor..." satırını cevap gelince ya da hata olunca kaldırır.
    private void RemoveLastChatLine()
    {
        if (_lastLineStart < 0 || _lastLineStart > _chatText.TextLength) return;
        _chatText.ReadOnly = false;
        _chatText.Select(_lastLineStart, _chatText.TextLength - _lastLineStart);
        _chatText.SelectedText = string.Empty;
        _chatText.ReadOnly = true;
        _lastLineStart = -1;
    }
}

using System.ComponentModel;
using System.Diagnostics;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraLayout;
using DevExpress.XtraReports.UI;
using TradingJournal.Core.Entities;
using TradingJournal.Services.Reports;
using TradingJournal.UI.Helpers;

namespace TradingJournal.UI.Reports;

// Ay seçip aylık raporu önizleme veya PDF olarak kaydetme penceresi.
// Rapor, üst çubukta seçili hesap için (ya da "Tüm hesaplar" için) hazırlanır.
[DesignerCategory("Code")]
public class MonthlyReportForm : XtraForm
{
    private readonly AppState _state;
    private readonly MonthlyReportService _service = new();

    private readonly LabelControl _account = new();
    private readonly ComboBoxEdit _month = new();
    private readonly LabelControl _hint = new() { AutoSizeMode = LabelAutoSizeMode.Vertical };
    private readonly SimpleButton _previewButton = new() { Text = "Önizle", Width = 110, Enabled = false };
    private readonly SimpleButton _pdfButton = new() { Text = "PDF olarak kaydet", Width = 140, Enabled = false };

    private List<Trade> _trades = new();
    private List<Account> _accounts = new();

    public MonthlyReportForm(AppState state)
    {
        _state = state;
        Text = "Aylık rapor";
        StartPosition = FormStartPosition.CenterParent;
        AppIcon.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 210);

        _month.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
        _hint.Text = "Rapor; özet, equity curve, günlük kâr/zarar, kırılımlar ve ayın tüm işlemlerini içerir. " +
                     "İşlemler kapandıkları aya sayılır.";
        _hint.Appearance.ForeColor = Theming.Theme.TextMuted;

        var layout = new LayoutControl { Dock = DockStyle.Fill };
        layout.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
        layout.Root.AddItem("Hesap", _account).TextLocation = DevExpress.Utils.Locations.Left;
        layout.Root.AddItem("Ay", _month).TextLocation = DevExpress.Utils.Locations.Left;
        LayoutControlItem hintItem = layout.Root.AddItem("", _hint);
        hintItem.TextVisible = false;

        _previewButton.Click += (_, _) => Preview();
        _pdfButton.Click += (_, _) => SavePdf();
        var closeButton = new SimpleButton { Text = "Kapat", Width = 90, DialogResult = DialogResult.Cancel };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(_pdfButton);
        buttons.Controls.Add(_previewButton);

        Controls.Add(layout);
        Controls.Add(buttons);
        CancelButton = closeButton;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            UseWaitCursor = true;
            _trades = await _state.LoadTradesAsync();
            List<Account> all = await _state.LoadAccountsAsync();
            _accounts = _state.AccountId is int id ? all.Where(a => a.Id == id).ToList() : all;
            _account.Text = _accounts.Count == 1 ? _accounts[0].Name : $"Tüm hesaplar ({_accounts.Count})";

            List<ReportMonth> months = _service.GetAvailableMonths(_trades);
            _month.Properties.Items.Clear();
            foreach (ReportMonth m in months)
                _month.Properties.Items.Add(m);
            if (months.Count > 0)
            {
                _month.SelectedIndex = 0;
                _previewButton.Enabled = _pdfButton.Enabled = true;
            }
            else
            {
                _hint.Text = "Bu hesapta henüz kapanmış işlem yok; rapor alınabilecek bir ay bulunmuyor.";
            }
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

    private MonthlyReport? CreateReport()
    {
        if (_month.SelectedItem is not ReportMonth month) return null;
        MonthlyReportData data = _service.Build(month.Month, _accounts, _trades);
        return new MonthlyReport(data);
    }

    private void Preview()
    {
        try
        {
            using MonthlyReport? report = CreateReport();
            if (report is null) return;
            using var tool = new ReportPrintTool(report);
            tool.ShowPreviewDialog();
        }
        catch (Exception ex)
        {
            ErrorHelper.Show(this, ex);
        }
    }

    private void SavePdf()
    {
        if (_month.SelectedItem is not ReportMonth month) return;
        string safeAccount = string.Concat(_account.Text.Split(Path.GetInvalidFileNameChars())).Trim();
        using var dialog = new SaveFileDialog
        {
            Title = "Aylık raporu PDF olarak kaydet",
            Filter = "PDF dosyası|*.pdf",
            FileName = $"EdgeBook-{month.Month:yyyy-MM}-{safeAccount}.pdf"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            UseWaitCursor = true;
            using MonthlyReport? report = CreateReport();
            if (report is null) return;
            report.ExportToPdf(dialog.FileName);
            UseWaitCursor = false;

            if (XtraMessageBox.Show(this, "Rapor kaydedildi. Şimdi açılsın mı?", "Aylık rapor",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
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
}

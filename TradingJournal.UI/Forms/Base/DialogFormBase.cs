using System.ComponentModel;
using DevExpress.XtraEditors;
using DevExpress.XtraLayout;
using TradingJournal.UI.Helpers;

namespace TradingJournal.UI.Forms.Base;

// Tüm "Kaydet / İptal" pencerelerinin ortak atası (soyut sınıf).
// Alanları alt alta dizen LayoutControl'ü ve alttaki butonları bir kez burada kurarız;
// alt sınıflar sadece kendi alanlarını ekleyip SaveAsync'i yazar (Template Method deseni).
[DesignerCategory("Code")]
public abstract class DialogFormBase : XtraForm
{
    protected LayoutControl FormLayout { get; }
    private readonly SimpleButton _saveButton;

    protected DialogFormBase(string title, int width = 460)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(width, 300);

        FormLayout = new LayoutControl { Dock = DockStyle.Fill };
        FormLayout.Root.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;

        _saveButton = new SimpleButton { Text = "Kaydet", Width = 100 };
        _saveButton.Click += async (_, _) => await OnSaveClickedAsync();

        var cancelButton = new SimpleButton { Text = "İptal", Width = 100, DialogResult = DialogResult.Cancel };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(_saveButton);

        Controls.Add(FormLayout);
        Controls.Add(buttonPanel);

        AcceptButton = _saveButton;
        CancelButton = cancelButton;
    }

    // Alt sınıfların form alanı eklemek için kullandığı yardımcı.
    protected LayoutControlItem AddField(string caption, Control editor)
    {
        LayoutControlItem item = FormLayout.Root.AddItem(caption, editor);
        item.TextLocation = DevExpress.Utils.Locations.Left;
        return item;
    }

    // Not alanı gibi çok satırlı editörler için sabit yükseklikli alan.
    protected LayoutControlItem AddField(string caption, Control editor, int height)
    {
        LayoutControlItem item = AddField(caption, editor);
        item.SizeConstraintsType = SizeConstraintsType.Custom;
        item.MinSize = new Size(100, height);
        item.MaxSize = new Size(0, height);
        return item;
    }

    // Alanların sayısına göre pencere yüksekliğini ayarlar.
    protected void FitHeight(int contentHeight) => ClientSize = new Size(ClientSize.Width, contentHeight + 48);

    // Kaydetme işini alt sınıf yapar. Geçersiz değer varsa DomainException fırlatmalıdır.
    protected abstract Task SaveAsync();

    private async Task OnSaveClickedAsync()
    {
        _saveButton.Enabled = false;
        try
        {
            await SaveAsync();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            // Hatalı alanda pencere kapanmaz, kullanıcı düzeltip tekrar dener.
            ErrorHelper.Show(this, ex);
        }
        finally
        {
            _saveButton.Enabled = true;
        }
    }
}

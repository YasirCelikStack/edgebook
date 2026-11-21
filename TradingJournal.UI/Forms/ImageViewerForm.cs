using System.ComponentModel;
using DevExpress.XtraEditors;
using TradingJournal.UI.Theming;

namespace TradingJournal.UI.Forms;

// Ekran görüntüsünü tam boyutta gösterir. Esc veya çift tık ile kapanır.
[DesignerCategory("Code")]
public class ImageViewerForm : XtraForm
{
    private readonly PictureBox _picture;

    public ImageViewerForm(string path, string title)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        KeyPreview = true;
        ShowInTaskbar = false;

        _picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Theme.Sidebar
        };

        // Dosyayı kilitlememek için bellekteki kopyasını gösteriyoruz.
        using (FileStream stream = File.OpenRead(path))
        using (Image original = Image.FromStream(stream))
            _picture.Image = new Bitmap(original);

        _picture.DoubleClick += (_, _) => Close();
        Controls.Add(_picture);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
            Close();
        base.OnKeyDown(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _picture.Image?.Dispose();
        base.Dispose(disposing);
    }
}
